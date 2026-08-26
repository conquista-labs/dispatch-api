using System.Text.Json.Serialization;
using Dispatch.Api.Endpoints;
using Dispatch.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

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

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapProtocoloEndpoints();

app.Run();
