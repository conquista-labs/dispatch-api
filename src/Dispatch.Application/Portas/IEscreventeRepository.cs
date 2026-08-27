using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEscreventeRepository
{
    Task<IReadOnlyCollection<Escrevente>> ObterTodosAsync(CancellationToken cancellationToken);
    void Adicionar(Escrevente escrevente);
}
