using System.Text;
using System.Text.Json.Serialization;
using Dispatch.Api.Endpoints;
using Dispatch.Api.OpenApi;
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
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags(OpenApiTags.Sistema)
    .AllowAnonymous();
app.MapAuthEndpoints();
app.MapProtocoloEndpoints();
app.MapConferenteEndpoints();
app.MapImportacaoEndpoints();
app.MapDistribuicaoEndpoints();
app.MapRegraAlcadaEndpoints();
app.MapEquipeEndpoints();
app.MapMinhaFilaEndpoints();
app.MapSugestaoEndpoints();
app.MapTipoAtoEndpoints();

app.Run();
