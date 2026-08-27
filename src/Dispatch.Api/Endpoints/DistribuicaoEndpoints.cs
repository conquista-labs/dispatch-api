using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class DistribuicaoEndpoints
{
    // Seção 5 do requisito: as duas faixas do semáforo são configuração do sistema. Ainda não
    // existe tabela de config (seção 8) — hardcoded aqui, com os mesmos valores de exemplo do
    // próprio documento, até isso existir.
    private static readonly TimeSpan FaixaAtencao = TimeSpan.FromHours(4);
    private static readonly TimeSpan FaixaUrgente = TimeSpan.FromMinutes(60);

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
                    visao.Pool.Select(p => ParaResumo(p, agora)).ToList(),
                    visao.Atribuidos.Select(p => ParaResumo(p, agora)).ToList(),
                    visao.EmConferencia.Select(p => ParaResumo(p, agora)).ToList(),
                    visao.Concluidos.Select(p => ParaResumo(p, agora)).ToList(),
                    visao.Excecoes.Select(p => ParaResumo(p, agora)).ToList(),
                    visao.PorConferente
                        .Select(g => new GrupoPorConferenteResponse(g.ConferenteId, g.Protocolos.Select(p => ParaResumo(p, agora)).ToList()))
                        .ToList()));
            })
            .WithName("ObterVisaoDistribuicao")
            .WithSummary("Três visões do mesmo conjunto de protocolos: por conferente, por status e exceções (RF-13).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces<VisaoDistribuicaoResponse>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
    }

    // RF-14: cada card leva protocolo/tipo/escrevente/etapa e o semáforo com o tempo restante.
    // Equipe não vai aqui — dá pra achar cruzando EscreventeId com GET /escreventes, que já
    // devolve o EquipeId de cada um; card não precisa repetir esse dado.
    private static ProtocoloResumo ParaResumo(Protocolo protocolo, DateTimeOffset agora) => new(
        protocolo.Id,
        protocolo.Numero,
        protocolo.TipoAtoId,
        protocolo.EscreventeId,
        protocolo.Etapa,
        protocolo.Status,
        protocolo.DonoId,
        protocolo.VencimentoEm,
        protocolo.MotivoExcecao,
        // RF-15: a observação do conferente aparece no card pra gestão.
        protocolo.Observacao,
        protocolo.VencimentoEm is { } vencimento ? Semaforo.Calcular(vencimento, agora, FaixaAtencao, FaixaUrgente) : null,
        protocolo.IniciadoEm);
}

public sealed record ProtocoloResumo(
    Guid Id,
    string Numero,
    Guid? TipoAtoId,
    Guid EscreventeId,
    Etapa Etapa,
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
