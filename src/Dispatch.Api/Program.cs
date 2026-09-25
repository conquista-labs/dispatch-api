using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Dispatch.Api;
using Dispatch.Api.Endpoints;
using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// dispatch-web roda em outra origem — sem isso o navegador bloqueia a chamada antes dela sair
// (CORS é regra de browser, curl/Postman nunca esbarram nisso, por isso não apareceu em nenhum
// teste ponta a ponta antes disso). Origem vem de config: "Cors:AllowedOrigin" no appsettings
// (Development já fixa localhost:5173); em produção (Fly.io) entra como variável de ambiente
// Cors__AllowedOrigin apontando pra URL real do dispatch-web (Netlify) — nunca hardcoded aqui,
// senão trocar de host do front exigiria recompilar a API.
const string CorsPolicy = "Cors";
var corsOrigin = builder.Configuration["Cors:AllowedOrigin"]
    ?? throw new InvalidOperationException("Configuração 'Cors:AllowedOrigin' ausente.");
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(corsOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
    options.AddSchemaTransformer<EnumSchemaTransformer>();
    options.AddDocumentTransformer<TagDescriptionsDocumentTransformer>();
});
builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection(JwtOptions.Secao).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Configuração 'Jwt' ausente.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Emissor,
            ValidateAudience = true,
            ValidAudience = jwt.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.ChaveDeAssinatura))
        };

        // RF-01k: "encerrar todas as sessões abertas" — JWT é stateless por padrão (sem jti,
        // sem blocklist), então isso só existe se checarmos aqui, a cada request autenticado,
        // contra Usuario.SessoesValidasApartirDe (bump feito só na troca de senha). 1 consulta
        // a mais por request — aceitável pro volume deste sistema (cartório interno). A mesma
        // consulta recusa conta desativada: antes, um token emitido antes de RemoverConferente ou
        // DesativarConta continuava valendo até expirar (8h).
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var usuarioId = context.Principal!.ObterUsuarioId();
                var usuarios = context.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>();
                var usuario = await usuarios.ObterPorIdAsync(usuarioId, context.HttpContext.RequestAborted);

                // ASP.NET Core 10 valida o token via Microsoft.IdentityModel.JsonWebTokens.JsonWebToken
                // (o handler novo), não mais o System.IdentityModel.Tokens.Jwt.JwtSecurityToken de
                // sempre — só descoberto rodando de verdade (dotnet build/test não pegam isso, o cast
                // errado só falha em runtime, na primeira chamada autenticada).
                var emitidoEm = ((Microsoft.IdentityModel.JsonWebTokens.JsonWebToken)context.SecurityToken).IssuedAt;
                if (usuario is null || !usuario.Ativo || DateTime.SpecifyKind(emitidoEm, DateTimeKind.Utc) < usuario.SessoesValidasApartirDe)
                {
                    context.Fail("Sessão encerrada — faça login novamente.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

// Enum como string no JSON ("PreConferencia", não 0) — mesma decisão que já vale pro banco
// (ver PrazoConversoes/HasConversion<string>): legível no Swagger e não quebra silenciosamente
// se alguém reordenar os valores do enum no C#.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Liberado em qualquer ambiente, não só Development — projeto pessoal pequeno, útil pra
// testar endpoint direto sem abrir o front. Só documenta a forma dos endpoints; usá-los de
// verdade continua exigindo o mesmo token de autenticação de sempre.
app.MapOpenApi();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Dispatch API v1"));

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthentication();

// RF-45 / ADR-0040 — conta com senha inicial: enquanto o token carrega a claim trocar_senha, ele só
// vale pra ver quem está logado e trocar a senha. Middleware (e não policy nem endpoint filter)
// porque é um ponto só pra TODA rota autenticada: policy teria de ser anexada a cada grupo, e a
// FallbackPolicy do ASP.NET só vale pra rota sem política própria — não pegaria as nossas. Fica
// entre UseAuthentication (o usuário já está identificado) e UseAuthorization.
app.Use(async (context, next) =>
{
    if (context.User.HasClaim(c => c.Type == ClaimsDoDispatch.TrocarSenha)
        && !RotasLiberadasComTrocaDeSenhaPendente.Contains(context.Request.Path.Value ?? ""))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(new
        {
            codigo = "troca_de_senha_obrigatoria",
            motivo = "troque a senha inicial antes de continuar",
        });
        return;
    }

    await next();
});

app.UseAuthorization();

// Liveness pura — é o healthCheckPath do render.yaml, decide se o Render considera o
// container de pé/roteável. De propósito NUNCA toca o banco: se checasse o Neon aqui, um cold
// start/hibernação momentânea do Neon derrubaria o health check e o Render poderia parar de
// rotear pro serviço (ou até reiniciar o container) por causa de uma lentidão transitória do
// banco, não da app em si — o container ficaria sem tráfego bem quando mais precisaria dele
// pra "acordar" a própria conexão.
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags(OpenApiTags.Sistema)
    .AllowAnonymous();

// Readiness — checa o banco de verdade (achado numa auditoria de resiliência: nada detectava
// "app de pé, Postgres inacessível"). Separado do /health de propósito (ver comentário acima);
// não é usado pelo healthCheckPath do Render, é pra diagnóstico manual/monitoramento externo.
app.MapGet("/health/db", async (DispatchDbContext dbContext, CancellationToken cancellationToken) =>
    {
        var conectado = await dbContext.Database.CanConnectAsync(cancellationToken);
        return conectado ? Results.Ok(new { status = "ok" }) : Results.Json(new { status = "erro" }, statusCode: 503);
    })
    .WithTags(OpenApiTags.Sistema)
    .AllowAnonymous();
app.MapAuthEndpoints();
app.MapTotpEndpoints();
app.MapRecuperacaoSenhaEndpoints();
app.MapProtocoloEndpoints();
app.MapConferenteEndpoints();
app.MapImportacaoEndpoints();
app.MapDistribuicaoEndpoints();
app.MapRegraAlcadaEndpoints();
app.MapEquipeEndpoints();
app.MapMinhaFilaEndpoints();
app.MapSugestaoEndpoints();
app.MapTipoAtoEndpoints();
app.MapDashboardEndpoints();
app.MapConfiguracaoEndpoints();
app.MapContaEndpoints();

// Só em Development — ver DevSeedEndpoints.cs. Nunca registrado em produção.
if (app.Environment.IsDevelopment())
{
    app.MapDevSeedEndpoints();
}

app.Run();

// Top-level statements geram uma classe Program internal — WebApplicationFactory<Program>
// (tests/Dispatch.Api.Tests) precisa dela pública pra subir a API em memória. Declarar a
// partial aqui é o jeito oficial de expor só o tipo, sem mudar nada do comportamento acima.
public partial class Program;

public partial class Program
{
    // Rotas que um token com troca de senha pendente ainda pode chamar (ver o middleware acima).
    private static readonly HashSet<string> RotasLiberadasComTrocaDeSenhaPendente =
        new(StringComparer.OrdinalIgnoreCase) { "/auth/me", "/auth/trocar-senha" };
}
