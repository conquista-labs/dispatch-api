using Dispatch.Domain;

namespace Dispatch.Application;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken);
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    // ListarConferentes usa isso pra resolver nome/e-mail em lote (Conferente não guarda
    // nome — é dado de Usuario) sem cair num N+1 de ObterPorIdAsync por conferente.
    Task<IReadOnlyCollection<Usuario>> ObterVariosPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> ExisteComEmailAsync(string email, CancellationToken cancellationToken);

    // Contas (RF-44): as contas de gestão (Distribuidora e Administrador), ativas ou não.
    Task<IReadOnlyCollection<Usuario>> ObterPorPapeisAsync(IReadOnlyCollection<Papel> papeis, CancellationToken cancellationToken);
    void Adicionar(Usuario usuario);
}
