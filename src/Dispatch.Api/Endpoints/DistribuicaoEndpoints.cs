using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class DistribuicaoEndpoints
{
    public static void MapDistribuicaoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/protocolos/distribuicao", async (
                Guid? loteImportacaoId,
                ObterVisaoDistribuicao casoDeUso,
                IRelogio relogio,
                CancellationToken cancellationToken) =>
            {
                var visao = await casoDeUso.ExecutarAsync(loteImportacaoId, cancellationToken);
                var agora = relogio.Agora;

                return Results.Ok(new VisaoDistribuicaoResponse(
                    visao.Pool.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList(),
                    visao.Atribuidos.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList(),
                    visao.EmConferencia.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList(),
                    visao.Concluidos.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList(),
                    visao.Excecoes.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList(),
                    visao.PorConferente
                        .Select(g => new GrupoPorConferenteResponse(
                            g.ConferenteId, g.Protocolos.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList()))
                        .ToList()));
            })
            .WithName("ObterVisaoDistribuicao")
            .WithSummary("Três visões do mesmo conjunto de protocolos: por conferente, por status e exceções (RF-13).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces<VisaoDistribuicaoResponse>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
    }
}

public sealed record ProtocoloResumo(
    Guid Id,
    string Numero,
    Guid? TipoAtoId,
    Guid EscreventeId,
    Etapa Etapa,
    Prioridade Prioridade,
    StatusProtocolo Status,
    Guid? DonoId,
    DateTimeOffset? VencimentoEm,
    string? MotivoExcecao,
    string? Observacao,
    FaixaSemaforo? Semaforo,
    // RF-21: o front calcula o cronômetro ao vivo (agora - IniciadoEm) — só existe depois que
    // IniciarConferencia roda, por isso nulo em qualquer status antes de "Conferindo".
    DateTimeOffset? IniciadoEm);

public sealed record GrupoPorConferenteResponse(Guid ConferenteId, IReadOnlyList<ProtocoloResumo> Protocolos);

public sealed record VisaoDistribuicaoResponse(
    IReadOnlyList<ProtocoloResumo> Pool,
    IReadOnlyList<ProtocoloResumo> Atribuidos,
    IReadOnlyList<ProtocoloResumo> EmConferencia,
    IReadOnlyList<ProtocoloResumo> Concluidos,
    IReadOnlyList<ProtocoloResumo> Excecoes,
    IReadOnlyList<GrupoPorConferenteResponse> PorConferente);
