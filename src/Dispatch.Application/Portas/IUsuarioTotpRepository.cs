using Dispatch.Domain;

namespace Dispatch.Application;

public interface IUsuarioTotpRepository
{
    Task<UsuarioTotp?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken);
    void Adicionar(UsuarioTotp usuarioTotp);
}
