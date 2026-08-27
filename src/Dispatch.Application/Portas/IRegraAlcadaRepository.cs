using Dispatch.Domain;

namespace Dispatch.Application;

public interface IRegraAlcadaRepository
{
    Task<IReadOnlyCollection<RegraAlcada>> ObterAtivasAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<RegraAlcada>> ObterTodasAsync(CancellationToken cancellationToken);
    Task<RegraAlcada?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    void Adicionar(RegraAlcada regra);

    // Ativar/desativar/remover mexem numa forma de persistência (RegraAlcadaRegistro) que o
    // Domain não conhece — por isso essas operações são assíncronas e resolvidas aqui dentro,
    // não fazendo "buscar depois mandar Adicionar de novo" como os outros agregados.
    Task<bool> AtivarAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> DesativarAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> RemoverAsync(Guid id, CancellationToken cancellationToken);
}
