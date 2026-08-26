using Dispatch.Domain;

namespace Dispatch.Application;

public interface ITipoAtoRepository
{
    Task<IReadOnlyCollection<TipoAto>> ObterTodosAsync(CancellationToken cancellationToken);
}
