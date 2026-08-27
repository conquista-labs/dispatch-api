using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class ImportacaoEndpoints
{
    public static void MapImportacaoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/protocolos/importar")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.Importacao);

        grupo.MapPost("/pre-visualizar", async (
                ImportarLoteRequest request,
                ImportarLote casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var resumo = await casoDeUso.PreVisualizarAsync(
                    ParaLinhas(request), request.Etapa, request.LinhaDeCorte, cancellationToken);
                return Results.Ok(resumo);
            })
            .WithName("PreVisualizarImportacao")
            .WithSummary("Roda a distribuição do lote inteiro sem gravar nada (RF-10/RF-11) — pra revisar antes de confirmar.")
            .Produces<ResumoImportacao>();

        grupo.MapPost("/confirmar", async (
                ImportarLoteRequest request,
                ImportarLote casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var resumo = await casoDeUso.ConfirmarAsync(
                    ParaLinhas(request), request.Etapa, request.LinhaDeCorte, cancellationToken);
                return Results.Ok(resumo);
            })
            .WithName("ConfirmarImportacao")
            .WithSummary("Roda a mesma distribuição e grava o resultado (RF-12).")
            .Produces<ResumoImportacao>();
    }

    private static List<LinhaImportacao> ParaLinhas(ImportarLoteRequest request) =>
        request.Linhas
            .Select(l => new LinhaImportacao(l.Protocolo, l.TipoAto, l.Escrevente, l.DataHoraAndamento))
            .ToList();
}

public sealed record ImportarLoteRequest(Etapa Etapa, DateTimeOffset LinhaDeCorte, IReadOnlyList<LinhaImportacaoRequest> Linhas);

public sealed record LinhaImportacaoRequest(string Protocolo, string TipoAto, string Escrevente, DateTimeOffset DataHoraAndamento);
