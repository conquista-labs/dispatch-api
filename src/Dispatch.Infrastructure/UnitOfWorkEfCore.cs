using Dispatch.Application;

namespace Dispatch.Infrastructure;

public sealed class UnitOfWorkEfCore(DispatchDbContext dbContext) : IUnitOfWork
{
    public Task SalvarAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
