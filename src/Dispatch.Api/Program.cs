using System.Text;
using System.Text.Json.Serialization;
using Dispatch.Api.Endpoints;
using Dispatch.Api.OpenApi;
using Dispatch.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Dispatch API v1"));
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithTags(OpenApiTags.Sistema)
    .AllowAnonymous();
app.MapAuthEndpoints();
app.MapProtocoloEndpoints();
app.MapConferenteEndpoints();

app.Run();
