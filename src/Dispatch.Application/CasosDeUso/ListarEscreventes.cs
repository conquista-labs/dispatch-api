using Dispatch.Domain;

namespace Dispatch.Application;

public sealed class ListarEscreventes(IEscreventeRepository escreventes)
{
    public Task<IReadOnlyCollection<Escrevente>> ExecutarAsync(CancellationToken cancellationToken = default) =>
        escreventes.ObterTodosAsync(cancellationToken);
}
