using Dispatch.Domain;

namespace Dispatch.Application;

public interface IConferenteRepository
{
    Task<IReadOnlyCollection<Conferente>> ObterNaEscalaAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Conferente>> ObterTodosAsync(CancellationToken cancellationToken);
    Task<Conferente?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    // Resolve "quem sou eu" a partir do token — o JWT carrega Usuario.Id (NameIdentifier),
    // não Conferente.Id, que é outra entidade. Toda ação de "Minha fila" começa por aqui.
    Task<Conferente?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken);
    void Adicionar(Conferente conferente);
}
