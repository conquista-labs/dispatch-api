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
                ProtocoloResumo ParaResumo(Protocolo p) => MinhaFilaEndpoints.ParaResumo(
                    p, agora, config.FaixaAtencao, config.FaixaUrgente, visao.NumeroDaConferencia.GetValueOrDefault(p.Id, 1));

                return Results.Ok(new VisaoDistribuicaoResponse(
                    visao.Pool.Select(ParaResumo).ToList(),
                    visao.Atribuidos.Select(ParaResumo).ToList(),
                    visao.EmConferencia.Select(ParaResumo).ToList(),
                    visao.Concluidos.Select(ParaResumo).ToList(),
                    visao.Excecoes.Select(ParaResumo).ToList(),
                    visao.PorConferente
                        .Select(g => new GrupoPorConferenteResponse(g.ConferenteId, g.Protocolos.Select(ParaResumo).ToList()))
                        .ToList(),
                    visao.ConcluidosHojePorConferente
                        .Select(c => new ConcluidosHojePorConferenteResponse(c.ConferenteId, c.Total))
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
    DateTimeOffset? IniciadoEm,
    // Pausa (pedido do dono, "a pessoa sai pra almoçar") — não nulo enquanto pausado; o front
    // usa isso pra trocar o cronômetro por "Pausado" + botão "Retomar" no card.
    DateTimeOffset? PausadoEm,
    // "N feitos hoje"/tempo de conferência no card de conferente (aba "Por status" →
    // Concluídos) — só existem depois de ConcluirConferencia, nulos em qualquer status antes
    // disso (mesma regra de IniciadoEm acima).
    DateTimeOffset? ConcluidoEm,
    TimeSpan? Duracao,
    // "Data de entrada" (RF-18f) — quando o ato chegou de verdade, pedido pelo dono pra
    // aparecer no card, não só no painel de detalhe (DetalheProtocoloResponse já tinha isso).
    DateTimeOffset AndamentoEm,
    // RF-24k: 1 = primeira conferência; 2+ = voltou depois de não aprovado (o front mostra
    // "↻ 2ª conferência"). Calculado na leitura (ADR-0038), nunca gravado.
    int NumeroDaConferencia);

public sealed record GrupoPorConferenteResponse(Guid ConferenteId, IReadOnlyList<ProtocoloResumo> Protocolos);

public sealed record ConcluidosHojePorConferenteResponse(Guid ConferenteId, int Total);

public sealed record VisaoDistribuicaoResponse(
    IReadOnlyList<ProtocoloResumo> Pool,
    IReadOnlyList<ProtocoloResumo> Atribuidos,
    IReadOnlyList<ProtocoloResumo> EmConferencia,
    IReadOnlyList<ProtocoloResumo> Concluidos,
    IReadOnlyList<ProtocoloResumo> Excecoes,
    IReadOnlyList<GrupoPorConferenteResponse> PorConferente,
    IReadOnlyList<ConcluidosHojePorConferenteResponse> ConcluidosHojePorConferente);
