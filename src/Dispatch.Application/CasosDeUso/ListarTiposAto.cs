using Dispatch.Domain;

namespace Dispatch.Application;

public sealed class ListarTiposAto(ITipoAtoRepository tiposAto)
{
    public Task<IReadOnlyCollection<TipoAto>> ExecutarAsync(CancellationToken cancellationToken = default) =>
        tiposAto.ObterTodosAsync(cancellationToken);
}
