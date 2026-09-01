using Dispatch.Domain;

namespace Dispatch.Application;

// RF-19: três colunas. "Pool disponível" já filtra pela alçada do conferente (reaproveita
// ResolvedorAlcada) — não é só "todo o pool", é só o que ele teria permissão de pegar.
public sealed record MinhaFila(
    IReadOnlyList<Protocolo> PoolDisponivel,
    IReadOnlyList<Protocolo> Atribuidos,
    IReadOnlyList<Protocolo> EmConferencia);

public sealed class ObterMinhaFila(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras)
{
    public async Task<MinhaFila> ExecutarAsync(Conferente conferente, CancellationToken cancellationToken = default)
    {
        var pool = await protocolos.ObterPoolAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var equipePorEscreventeId = (await escreventes.ObterTodosAsync(cancellationToken))
            .ToDictionary(e => e.Id, e => e.EquipeId);

        var poolDisponivel = pool
            .Where(p => VerificadorDeAlcada.TemAlcada(conferente, p, equipePorEscreventeId.GetValueOrDefault(p.EscreventeId), regrasAtivas))
            .ToList();

        var atribuidos = await protocolos.ObterAtribuidosAAsync(conferente.Id, cancellationToken);
        var emConferencia = await protocolos.ObterEmConferenciaPorConferenteAsync(conferente.Id, cancellationToken);

        return new MinhaFila(poolDisponivel, atribuidos.ToList(), emConferencia.ToList());
    }
}
