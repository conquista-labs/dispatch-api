using Dispatch.Domain;

namespace Dispatch.Application;

// RF-19: três colunas. "Pool disponível" já filtra pela alçada do conferente (reaproveita
// ResolvedorAlcada) — não é só "todo o pool", é só o que ele teria permissão de pegar.
public sealed record MinhaFila(
    IReadOnlyList<Protocolo> PoolDisponivel,
    IReadOnlyList<Protocolo> Atribuidos,
    IReadOnlyList<Protocolo> EmConferencia,
    // RF-24k: protocoloId → nº da conferência (1 = primeira), das três colunas.
    IReadOnlyDictionary<Guid, int> NumeroDaConferencia);

public sealed class ObterMinhaFila(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto)
{
    public async Task<MinhaFila> ExecutarAsync(Conferente conferente, CancellationToken cancellationToken = default)
    {
        var pool = await protocolos.ObterPoolAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var equipePorEscreventeId = (await escreventes.ObterTodosAsync(cancellationToken))
            .ToDictionary(e => e.Id, e => e.EquipeId);
        var tipoPorId = (await tiposAto.ObterTodosAsync(cancellationToken)).ToDictionary(t => t.Id);

        var poolDisponivel = pool
            // Tipo desconhecido (TipoAtoId nulo) nunca tem alvo pra resolver regra nenhuma —
            // fica fora do pool disponível de qualquer conferente (mesma exceção que o motor
            // de distribuição já usa).
            .Where(p => p.TipoAtoId is { } tipoAtoId && tipoPorId.TryGetValue(tipoAtoId, out var tipo)
                && VerificadorDeAlcada.TemAlcada(conferente, p, tipo, equipePorEscreventeId.GetValueOrDefault(p.EscreventeId), regrasAtivas))
            // Quem tá vencendo primeiro fica no topo — sem isso a ordem é a do banco, que não é
            // garantida sem ORDER BY (mesma armadilha já documentada em ListarConferentes).
            .OrderBy(p => p.VencimentoEm ?? DateTimeOffset.MaxValue)
            .ToList();

        // Mesmo raciocínio do pool acima: sem ORDER BY a ordem não é garantida — pedido do
        // dono pra "Atribuídas a você" também ficar por vencimento, quem tá vencendo primeiro
        // no topo (mesmo critério do pool, já estabelecido).
        var atribuidos = (await protocolos.ObterAtribuidosAAsync(conferente.Id, cancellationToken))
            .OrderBy(p => p.VencimentoEm ?? DateTimeOffset.MaxValue)
            .ToList();
        var emConferencia = await protocolos.ObterEmConferenciaPorConferenteAsync(conferente.Id, cancellationToken);

        var numeroDaConferencia = await NumeroDaConferenciaEmLote.CalcularAsync(
            protocolos, [.. poolDisponivel, .. atribuidos, .. emConferencia], cancellationToken);

        return new MinhaFila(poolDisponivel, atribuidos, emConferencia.ToList(), numeroDaConferencia);
    }
}
