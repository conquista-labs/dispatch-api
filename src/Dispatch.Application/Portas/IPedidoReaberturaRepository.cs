using Dispatch.Domain;

namespace Dispatch.Application;

public interface IPedidoReaberturaRepository
{
    Task<PedidoReabertura?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    // RF-24b: garante "só um pedido pendente por protocolo por vez" — checado antes de criar.
    Task<PedidoReabertura?> ObterPendentePorProtocoloAsync(Guid protocoloId, CancellationToken cancellationToken);

    // RF-24c: alimenta a seção "Pedidos de reabertura" da aba de Exceções.
    Task<IReadOnlyCollection<PedidoReabertura>> ObterPendentesAsync(CancellationToken cancellationToken);

    // RF-24: "concluídos hoje" (Minha fila) precisa saber, por protocolo, se há pedido
    // pendente — em lote, pra não fazer uma consulta por item da lista.
    Task<IReadOnlyCollection<PedidoReabertura>> ObterPendentesPorProtocolosAsync(
        IReadOnlyCollection<Guid> protocoloIds, CancellationToken cancellationToken);

    void Adicionar(PedidoReabertura pedido);
}
