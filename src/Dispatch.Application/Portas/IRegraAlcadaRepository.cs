using Dispatch.Domain;

namespace Dispatch.Application;

public interface IRegraAlcadaRepository
{
    Task<IReadOnlyCollection<RegraAlcada>> ObterAtivasAsync(CancellationToken cancellationToken);
}
