using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class EquipeRepository(DispatchDbContext dbContext) : IEquipeRepository
{
    public async Task<IReadOnlyCollection<Equipe>> ObterTodasAsync(CancellationToken cancellationToken) =>
        await dbContext.Equipes.ToListAsync(cancellationToken);

    public async Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Equipes.SingleOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Adicionar(Equipe equipe) => dbContext.Equipes.Add(equipe);
}
