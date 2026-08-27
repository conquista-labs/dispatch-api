using Dispatch.Domain;

namespace Dispatch.Application;

public sealed class ListarRegrasAlcada(IRegraAlcadaRepository regras)
{
    public Task<IReadOnlyCollection<RegraAlcada>> ExecutarAsync(CancellationToken cancellationToken = default) =>
        regras.ObterTodasAsync(cancellationToken);
}
