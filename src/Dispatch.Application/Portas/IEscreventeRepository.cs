using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEscreventeRepository
{
    Task<IReadOnlyCollection<Escrevente>> ObterTodosAsync(CancellationToken cancellationToken);
    Task<Escrevente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    void Adicionar(Escrevente escrevente);
}
