using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class TipoAtoRepository(DispatchDbContext dbContext) : ITipoAtoRepository
{
    public async Task<IReadOnlyCollection<TipoAto>> ObterTodosAsync(CancellationToken cancellationToken) =>
        await dbContext.TiposAto.ToListAsync(cancellationToken);

    public async Task<TipoAto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.TiposAto.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void Adicionar(TipoAto tipoAto) => dbContext.TiposAto.Add(tipoAto);

    public void Remover(TipoAto tipoAto) => dbContext.TiposAto.Remove(tipoAto);
}
