using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class ConferenteRepository(DispatchDbContext dbContext) : IConferenteRepository
{
    public async Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken) =>
        await dbContext.Conferentes.Where(c => c.NaEscala).ToListAsync(cancellationToken);
}
