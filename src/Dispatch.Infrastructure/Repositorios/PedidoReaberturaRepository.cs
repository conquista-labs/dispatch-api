using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class PedidoReaberturaRepository(DispatchDbContext dbContext) : IPedidoReaberturaRepository
{
    public async Task<PedidoReabertura?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.PedidosReabertura.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<PedidoReabertura?> ObterPendentePorProtocoloAsync(Guid protocoloId, CancellationToken cancellationToken) =>
        await dbContext.PedidosReabertura.SingleOrDefaultAsync(
            p => p.ProtocoloId == protocoloId && p.Status == StatusPedidoReabertura.Pendente, cancellationToken);

    public async Task<IReadOnlyCollection<PedidoReabertura>> ObterPendentesAsync(CancellationToken cancellationToken) =>
        await dbContext.PedidosReabertura.Where(p => p.Status == StatusPedidoReabertura.Pendente).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PedidoReabertura>> ObterPendentesPorProtocolosAsync(
        IReadOnlyCollection<Guid> protocoloIds, CancellationToken cancellationToken) =>
        await dbContext.PedidosReabertura
            .Where(p => p.Status == StatusPedidoReabertura.Pendente && protocoloIds.Contains(p.ProtocoloId))
            .ToListAsync(cancellationToken);

    public void Adicionar(PedidoReabertura pedido) => dbContext.PedidosReabertura.Add(pedido);
}
