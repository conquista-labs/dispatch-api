using Dispatch.Domain;

namespace Dispatch.Application;

public interface IConferenteRepository
{
    Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken);
    Task<Conferente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    void Adicionar(Conferente conferente);
}
