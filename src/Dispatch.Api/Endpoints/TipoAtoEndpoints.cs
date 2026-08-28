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

        app.MapPost("/tipos-ato", async (CriarTipoAtoRequest request, CriarTipoAto casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(request.Nome, cancellationToken);
                return resultado switch
                {
                    ResultadoCriarTipoAto.Sucesso sucesso => Results.Created($"/tipos-ato/{sucesso.TipoAtoId}", new CriarTipoAtoResponse(sucesso.TipoAtoId)),
                    ResultadoCriarTipoAto.JaExiste => Results.Conflict(new { motivo = "já existe um tipo de ato com esse nome" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CriarTipoAto")
            .WithSummary("Cadastro manual — complementa o cadastro automático que a importação já faz (nome sai normalizado).")
            .WithTags(OpenApiTags.CentralDeRegras)
            .Produces<CriarTipoAtoResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
    }

    private static TipoAtoResponse ParaResponse(TipoAto tipoAto) => new(tipoAto.Id, tipoAto.Nome, tipoAto.Ativo);
}

public sealed record TipoAtoResponse(Guid Id, string Nome, bool Ativo);

public sealed record CriarTipoAtoRequest(string Nome);

public sealed record CriarTipoAtoResponse(Guid TipoAtoId);
