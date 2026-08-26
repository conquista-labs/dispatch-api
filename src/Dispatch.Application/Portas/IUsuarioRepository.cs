using Dispatch.Domain;

namespace Dispatch.Application;

public interface IUsuarioRepository
{
    Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken);
}
