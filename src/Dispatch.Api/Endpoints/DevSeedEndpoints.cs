using Dispatch.Api.OpenApi;
using Dispatch.Application;

namespace Dispatch.Api.Endpoints;

// Só mapeado quando app.Environment.IsDevelopment() (ver Program.cs) — nunca existe em
// produção, então "senha previsível" (SemearContasE2E.Senha) não é um risco lá. Existe pra
// dispatch-web/e2e/global-setup.ts chamar antes da suíte rodar, garantindo o "chão" de login
// que os specs usam sem depender de o banco local já ter as contas certas por acaso (dado de
// dev antigo, clone de produção, banco vazio — tanto faz).
public static class DevSeedEndpoints
{
    public static void MapDevSeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev/seed-e2e", async (SemearContasE2E casoDeUso, CancellationToken cancellationToken) =>
            {
                await casoDeUso.ExecutarAsync(cancellationToken);
                return Results.NoContent();
            })
            .WithName("SemearContasE2E")
            .WithSummary("Só em Development — garante as contas fixas que a suíte e2e do dispatch-web usa pra logar.")
            .WithTags(OpenApiTags.Sistema)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);
    }
}
