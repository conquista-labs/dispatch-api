using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class EquipeRepository(DispatchDbContext dbContext) : IEquipeRepository
{
    public async Task<IReadOnlyCollection<Equipe>> ObterTodasAsync(CancellationToken cancellationToken) =>
        await dbContext.Equipes.ToListAsync(cancellationToken);
}
