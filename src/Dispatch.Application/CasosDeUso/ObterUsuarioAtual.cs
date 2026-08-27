using Dispatch.Domain;

namespace Dispatch.Application;

// GET /auth/me: reidrata a sessão a partir do token (RNF-04 na prática — o front nunca abre
// o JWT na mão, só manda o Authorization header e confia na resposta do servidor). Pass-through
// fino, mas existe pra Api nunca injetar IUsuarioRepository direto — mesma regra de sempre.
public sealed class ObterUsuarioAtual(IUsuarioRepository usuarios)
{
    public Task<Usuario?> ExecutarAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        usuarios.ObterPorIdAsync(usuarioId, cancellationToken);
}
