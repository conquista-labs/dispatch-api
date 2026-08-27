using Dispatch.Domain;

namespace Dispatch.Application;

public sealed class ListarEquipes(IEquipeRepository equipes)
{
    public Task<IReadOnlyCollection<Equipe>> ExecutarAsync(CancellationToken cancellationToken = default) =>
        equipes.ObterTodasAsync(cancellationToken);
}
