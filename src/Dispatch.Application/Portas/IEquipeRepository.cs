using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEquipeRepository
{
    Task<IReadOnlyCollection<Equipe>> ObterTodasAsync(CancellationToken cancellationToken);
    Task<Equipe?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    void Adicionar(Equipe equipe);
}
