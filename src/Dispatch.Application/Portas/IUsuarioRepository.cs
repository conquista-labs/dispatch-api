using Dispatch.Domain;

namespace Dispatch.Application;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken);
    Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExisteComEmailAsync(string email, CancellationToken cancellationToken);
    void Adicionar(Usuario usuario);
}
