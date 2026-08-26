using Dispatch.Domain;

namespace Dispatch.Application;

public interface IConferenteRepository
{
    Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken);
}
