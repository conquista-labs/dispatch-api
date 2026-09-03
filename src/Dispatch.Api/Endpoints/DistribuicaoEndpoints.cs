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
                ObterConfiguracao obterConfiguracao,
                IRelogio relogio,
                CancellationToken cancellationToken) =>
            {
                var visao = await casoDeUso.ExecutarAsync(loteImportacaoId, cancellationToken);
                var agora = relogio.Agora;
                var config = await obterConfiguracao.ExecutarAsync(cancellationToken);
                ProtocoloResumo ParaResumo(Protocolo p) => MinhaFilaEndpoints.ParaResumo(p, agora, config.FaixaAtencao, config.FaixaUrgente);

                return Results.Ok(new VisaoDistribuicaoResponse(
                    visao.Pool.Select(ParaResumo).ToList(),
                    visao.Atribuidos.Select(ParaResumo).ToList(),
                    visao.EmConferencia.Select(ParaResumo).ToList(),
                    visao.Concluidos.Select(ParaResumo).ToList(),
                    visao.Excecoes.Select(ParaResumo).ToList(),
                    visao.PorConferente
                        .Select(g => new GrupoPorConferenteResponse(g.ConferenteId, g.Protocolos.Select(ParaResumo).ToList()))
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
