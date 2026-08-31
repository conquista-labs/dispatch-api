using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

// Seção 7 / RF-39 a RF-41 — aba "Aprendizado" da Central de regras. Tudo aqui é Distribuidora
// (RF-40: "nenhuma regra é aplicada sem aprovação humana explícita", RNF-03).
public static class SugestaoEndpoints
{
    public static void MapSugestaoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/sugestoes")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.CentralDeRegras);

        grupo.MapPost("/gerar", async (GerarSugestoes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok(new GerarSugestoesResponse(await casoDeUso.ExecutarAsync(cancellationToken))))
            .WithName("GerarSugestoes")
            .WithSummary("Roda o \"job diário\" sob demanda — recalcula as 4 propostas da seção 7 contra os dados atuais.")
            .Produces<GerarSugestoesResponse>();

        grupo.MapGet("/", async (ListarSugestoesPendentes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarSugestoesPendentes")
            .WithSummary("Fila de propostas pendentes, cada uma com evidência e ocorrências (RF-39).")
            .Produces<IReadOnlyList<SugestaoResponse>>();

        grupo.MapGet("/historico", async (ListarHistoricoSugestoes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarHistoricoSugestoes")
            .WithSummary("O que já foi aplicado ou descartado, com o efeito de cada decisão (RF-41).")
            .Produces<IReadOnlyList<SugestaoResponse>>();

        grupo.MapPost("/{id:guid}/aplicar", async (Guid id, AplicarSugestao casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return resultado switch
                {
                    ResultadoAplicarSugestao.Sucesso => Results.NoContent(),
                    ResultadoAplicarSugestao.NaoEncontrada => Results.NotFound(new { motivo = "sugestão não encontrada" }),
                    ResultadoAplicarSugestao.NaoEstaPendente => Results.Conflict(new { motivo = "sugestão não está pendente" }),
                    ResultadoAplicarSugestao.ReferenciaNaoEncontrada =>
                        Results.Conflict(new { motivo = "equipe ou escrevente referenciado não existe mais" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("AplicarSugestao")
            .WithSummary("Executa a mudança de verdade (classifica tipo / muda prazo / aloca escrevente / cria regra) — RF-40.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/descartar", async (Guid id, DescartarSugestao casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("DescartarSugestao")
            .WithSummary("Silencia a proposta — não reaparece por um tempo (descarte com memória) — RF-40.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static SugestaoResponse ParaResponse(Sugestao sugestao) => new(
        sugestao.Id,
        sugestao.Payload.GetType().Name,
        sugestao.Chave,
        sugestao.Evidencia,
        sugestao.Ocorrencias,
        sugestao.IndiceConfianca,
        sugestao.Status,
        sugestao.CriadaEm,
        sugestao.AtualizadaEm,
        sugestao.DecididaEm,
        sugestao.DescartarAte,
        (sugestao.Payload as PayloadSugestao.TipoDesconhecido)?.NomeTipo,
        (sugestao.Payload as PayloadSugestao.TipoDesconhecido)?.NivelSugerido,
        (sugestao.Payload as PayloadSugestao.PrazoIrreal)?.EquipeId,
        (sugestao.Payload as PayloadSugestao.PrazoIrreal)?.Etapa,
        (sugestao.Payload as PayloadSugestao.PrazoIrreal)?.PrazoSugerido,
        (sugestao.Payload as PayloadSugestao.EscreventeOrfao)?.EscreventeId,
        (sugestao.Payload as PayloadSugestao.EscreventeOrfao)?.EquipeSugeridaId,
        (sugestao.Payload as PayloadSugestao.RiscoQualidade)?.TipoAtoId,
        (sugestao.Payload as PayloadSugestao.RiscoQualidade)?.NivelRestrito);
}

public sealed record GerarSugestoesResponse(int NovasSugestoes);

public sealed record SugestaoResponse(
    Guid Id,
    string Tipo,
    string Chave,
    string Evidencia,
    int Ocorrencias,
    double IndiceConfianca,
    StatusSugestao Status,
    DateTimeOffset CriadaEm,
    DateTimeOffset AtualizadaEm,
    DateTimeOffset? DecididaEm,
    DateTimeOffset? DescartarAte,
    string? TipoDesconhecidoNomeTipo,
    Nivel? TipoDesconhecidoNivelSugerido,
    Guid? PrazoIrrealEquipeId,
    Etapa? PrazoIrrealEtapa,
    TipoPrazo? PrazoIrrealPrazoSugerido,
    Guid? EscreventeOrfaoEscreventeId,
    Guid? EscreventeOrfaoEquipeSugeridaId,
    Guid? RiscoQualidadeTipoAtoId,
    Nivel? RiscoQualidadeNivelRestrito);
