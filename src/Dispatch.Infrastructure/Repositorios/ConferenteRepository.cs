using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class ConferenteRepository(DispatchDbContext dbContext) : IConferenteRepository
{
    public async Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken) =>
        await dbContext.Conferentes.Where(c => c.NaEscala).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Conferente>> ObterTodosAsync(CancellationToken cancellationToken) =>
        await dbContext.Conferentes.ToListAsync(cancellationToken);

    public async Task<Conferente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Conferentes.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<Conferente?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        await dbContext.Conferentes.SingleOrDefaultAsync(c => c.UsuarioId == usuarioId, cancellationToken);

    public void Adicionar(Conferente conferente) => dbContext.Conferentes.Add(conferente);
}
