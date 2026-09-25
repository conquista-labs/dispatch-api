using Dispatch.Domain;

namespace Dispatch.Application;

// Pedido do dono: uma distribuidora que também confere pessoalmente (mesma conta, os dois
// papéis) — sem transformar Usuario.Papel numa lista no Domain (zero migration). "Também é
// conferente" é derivado de já existir um Conferente vinculado a esse UsuarioId, o mesmo dado
// que o resto do sistema já usa pra saber "essa pessoa confere" (alçada, fila, dashboard
// restrito). Usado por Autenticar (JWT) e ObterUsuarioAtual (GET /auth/me) — os dois lugares
// que precisam do papel efetivo de alguém, não só o Usuario.Papel cru.
//
// Administrador carrega também a claim Distribuidora (ADR-0039): os ~40 RequireRole(Distribuidora)
// existentes passam a significar "tem acesso de gestão" e o admin passa em todos sem tocar neles;
// o que é só do admin ganha RequireRole(Administrador) na rota, que soma (E) com a do grupo. A
// ordem importa: o front usa papeis[0] como papel principal.
internal static class PapeisEfetivos
{
    public static async Task<IReadOnlyList<Papel>> ObterAsync(
        Usuario usuario, IConferenteRepository conferentes, CancellationToken cancellationToken)
    {
        if (usuario.Papel == Papel.Conferente)
        {
            return [Papel.Conferente];
        }

        List<Papel> papeis = usuario.Papel == Papel.Administrador
            ? [Papel.Administrador, Papel.Distribuidora]
            : [usuario.Papel];

        if (await conferentes.ObterPorUsuarioIdAsync(usuario.Id, cancellationToken) is not null)
        {
            papeis.Add(Papel.Conferente);
        }

        return papeis;
    }
}
