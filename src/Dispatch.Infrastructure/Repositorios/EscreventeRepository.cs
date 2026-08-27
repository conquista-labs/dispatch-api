using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class EscreventeRepository(DispatchDbContext dbContext) : IEscreventeRepository
{
    public async Task<IReadOnlyCollection<Escrevente>> ObterTodosAsync(CancellationToken cancellationToken) =>
        await dbContext.Escreventes.ToListAsync(cancellationToken);

    public void Adicionar(Escrevente escrevente) => dbContext.Escreventes.Add(escrevente);
}
