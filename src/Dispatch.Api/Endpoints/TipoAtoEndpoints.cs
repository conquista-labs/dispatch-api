using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class TipoAtoEndpoints
{
    public static void MapTipoAtoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tipos-ato", async (ListarTiposAto casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarTiposAto")
            .WithSummary("Catálogo de tipos de ato — usado pra resolver nome no alvo de uma regra de alçada (RF-31).")
            .WithTags(OpenApiTags.CentralDeRegras)
            .Produces<IReadOnlyList<TipoAtoResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
    }

    private static TipoAtoResponse ParaResponse(TipoAto tipoAto) => new(tipoAto.Id, tipoAto.Nome, tipoAto.Ativo);
}

public sealed record TipoAtoResponse(Guid Id, string Nome, bool Ativo);
