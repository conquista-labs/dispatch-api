using Dispatch.Domain;

namespace Dispatch.Application;

// O pool que UM conferente enxerga (RF-19: só o que está na alçada dele), já na ordem da vez
// (OrdemDoPool), e a regra do pool calculada em cima dele. Um lugar só porque duas pontas dependem de
// ser exatamente a mesma lista: a leitura (ObterMinhaFila → `poolDisponivel` + `regraDoPool.proximoId`)
// e a ação (PegarProtocolo → "é o primeiro da vez?"). Se cada uma filtrasse ou ordenasse do seu jeito,
// o botão "Pegar este" que o front mostra e o que o servidor aceita discordariam (ADR-0046).
internal sealed class PoolDoConferente(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IConfiguracaoRepository configuracao)
{
    public async Task<IReadOnlyList<Protocolo>> ObterDisponivelAsync(Conferente conferente, CancellationToken cancellationToken)
    {
        var pool = await protocolos.ObterPoolAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var equipePorEscreventeId = (await escreventes.ObterTodosAsync(cancellationToken))
            .ToDictionary(e => e.Id, e => e.EquipeId);
        var tipoPorId = (await tiposAto.ObterTodosAsync(cancellationToken)).ToDictionary(t => t.Id);

        return OrdemDoPool.Ordenar(pool
            // Tipo desconhecido (TipoAtoId nulo) nunca tem alvo pra resolver regra nenhuma — fica fora
            // do pool disponível de qualquer conferente (mesma exceção que o motor de distribuição usa).
            .Where(p => p.TipoAtoId is { } tipoAtoId && tipoPorId.TryGetValue(tipoAtoId, out var tipo)
                && VerificadorDeAlcada.TemAlcada(conferente, p, tipo, equipePorEscreventeId.GetValueOrDefault(p.EscreventeId), regrasAtivas)));
    }

    // "Na mão" = atribuídos + em conferência (pausado é Conferindo, então conta) — tudo que está com
    // ele e não terminou. Recebe as duas coleções já lidas quando quem chama também precisa delas.
    public async Task<RegraDoPool> CalcularRegraAsync(
        int naMao, IReadOnlyList<Protocolo> poolDisponivel, CancellationToken cancellationToken)
    {
        var config = await configuracao.ObterAsync(cancellationToken);
        return RegraDoPool.Calcular(config.PoolEmOrdemObrigatoria, config.LimiteDeAtosNaMao, naMao, poolDisponivel);
    }
}
