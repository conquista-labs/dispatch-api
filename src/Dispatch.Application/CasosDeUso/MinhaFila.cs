using Dispatch.Domain;

namespace Dispatch.Application;

// RF-19: três colunas. "Pool disponível" já filtra pela alçada do conferente (reaproveita
// ResolvedorAlcada) — não é só "todo o pool", é só o que ele teria permissão de pegar — e vem na
// ordem da vez (OrdemDoPool, ADR-0046).
public sealed record MinhaFila(
    IReadOnlyList<Protocolo> PoolDisponivel,
    IReadOnlyList<Protocolo> Atribuidos,
    IReadOnlyList<Protocolo> EmConferencia,
    // RF-24k: protocoloId → nº da conferência (1 = primeira), das três colunas.
    IReadOnlyDictionary<Guid, int> NumeroDaConferencia,
    // ADR-0046: ordem obrigatória, limite na mão, quantos ele tem e o próximo da vez.
    RegraDoPool RegraDoPool);

public sealed class ObterMinhaFila(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IConfiguracaoRepository configuracao)
{
    private readonly PoolDoConferente _pool = new(protocolos, escreventes, regras, tiposAto, configuracao);

    public async Task<MinhaFila> ExecutarAsync(Conferente conferente, CancellationToken cancellationToken = default)
    {
        var poolDisponivel = await _pool.ObterDisponivelAsync(conferente, cancellationToken);

        // Sem ORDER BY a ordem não é garantida — pedido do dono pra "Atribuídas a você" ficar por
        // vencimento, quem tá vencendo primeiro no topo. Continua assim (não usa a ordem da vez): as
        // atribuídas são livres, o conferente inicia a que quiser (ADR-0046).
        var atribuidos = (await protocolos.ObterAtribuidosAAsync(conferente.Id, cancellationToken))
            .OrderBy(p => p.VencimentoEm ?? DateTimeOffset.MaxValue)
            .ToList();
        var emConferencia = await protocolos.ObterEmConferenciaPorConferenteAsync(conferente.Id, cancellationToken);

        var numeroDaConferencia = await NumeroDaConferenciaEmLote.CalcularAsync(
            protocolos, [.. poolDisponivel, .. atribuidos, .. emConferencia], cancellationToken);
        var regraDoPool = await _pool.CalcularRegraAsync(atribuidos.Count + emConferencia.Count, poolDisponivel, cancellationToken);

        return new MinhaFila(poolDisponivel, atribuidos, emConferencia.ToList(), numeroDaConferencia, regraDoPool);
    }
}
