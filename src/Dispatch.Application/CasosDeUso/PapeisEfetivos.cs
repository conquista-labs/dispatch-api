using Dispatch.Domain;

namespace Dispatch.Application;

// Pedido do dono: uma distribuidora que também confere pessoalmente (mesma conta, os dois
// papéis) — sem transformar Usuario.Papel numa lista no Domain (zero migration). "Também é
// conferente" é derivado de já existir um Conferente vinculado a esse UsuarioId, o mesmo dado
// que o resto do sistema já usa pra saber "essa pessoa confere" (alçada, fila, dashboard
// restrito). Usado por Autenticar (JWT) e ObterUsuarioAtual (GET /auth/me) — os dois lugares
// que precisam do papel efetivo de alguém, não só o Usuario.Papel cru.
internal static class PapeisEfetivos
{
    public static async Task<IReadOnlyList<Papel>> ObterAsync(
        Usuario usuario, IConferenteRepository conferentes, CancellationToken cancellationToken)
    {
        if (usuario.Papel == Papel.Conferente)
        {
            return [Papel.Conferente];
        }

        var conferente = await conferentes.ObterPorUsuarioIdAsync(usuario.Id, cancellationToken);
        return conferente is null ? [usuario.Papel] : [usuario.Papel, Papel.Conferente];
    }
}
