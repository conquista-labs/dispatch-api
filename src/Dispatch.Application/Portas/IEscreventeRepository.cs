using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEscreventeRepository
{
    Task<IReadOnlyCollection<Escrevente>> ObterTodosAsync(CancellationToken cancellationToken);
    // Filtra no banco, não traz a tabela inteira pra filtrar em memória — usado por
    // RecalculoDeVencimentos (RF-38), que só precisa dos escreventes de UMA equipe.
    Task<IReadOnlyCollection<Escrevente>> ObterPorEquipeIdAsync(Guid equipeId, CancellationToken cancellationToken);
    Task<Escrevente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    void Adicionar(Escrevente escrevente);
}
