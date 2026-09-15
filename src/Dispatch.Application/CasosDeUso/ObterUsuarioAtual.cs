using Dispatch.Domain;

namespace Dispatch.Application;

// GET /auth/me: reidrata a sessão a partir do token (RNF-04 na prática — o front nunca abre
// o JWT na mão, só manda o Authorization header e confia na resposta do servidor). Devolve
// Papeis (não Usuario.Papel cru) pelo mesmo motivo do Autenticar — ver PapeisEfetivos.
public sealed class ObterUsuarioAtual(IUsuarioRepository usuarios, IConferenteRepository conferentes)
{
    public async Task<UsuarioAtual?> ExecutarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, cancellationToken);
        if (usuario is null)
        {
            return null;
        }

        var papeis = await PapeisEfetivos.ObterAsync(usuario, conferentes, cancellationToken);
        return new UsuarioAtual(usuario.Id, usuario.Nome, usuario.Email, papeis);
    }
}

public sealed record UsuarioAtual(Guid Id, string Nome, string Email, IReadOnlyList<Papel> Papeis);
