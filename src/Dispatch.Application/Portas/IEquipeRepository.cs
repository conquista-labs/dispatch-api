using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEquipeRepository
{
    Task<IReadOnlyCollection<Equipe>> ObterTodasAsync(CancellationToken cancellationToken);
}
