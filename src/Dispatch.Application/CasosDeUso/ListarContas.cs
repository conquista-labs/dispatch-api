using Dispatch.Domain;

namespace Dispatch.Application;

// RF-44: as contas de gestão (Administrador e Distribuidora), ativas e inativas, com "também
// confere" (existe um Conferente vinculado ao Usuario — PapeisEfetivos usa o mesmo dado) e "você"
// na própria linha. Ordenado por nome e Id (sem ORDER BY, a lista pula de posição a cada refetch).
public sealed class ListarContas(IUsuarioRepository usuarios, IConferenteRepository conferentes)
{
    private static readonly Papel[] PapeisDeGestao = [Papel.Administrador, Papel.Distribuidora];

    public async Task<IReadOnlyList<ContaResumo>> ExecutarAsync(Guid usuarioLogadoId, CancellationToken cancellationToken = default)
    {
        var contas = await usuarios.ObterPorPapeisAsync(PapeisDeGestao, cancellationToken);
        var quemConfere = (await conferentes.ObterTodosAsync(cancellationToken)).Select(c => c.UsuarioId).ToHashSet();

        return contas
            .Select(u => new ContaResumo(u.Id, u.Nome, u.Email, u.Papel, u.Ativo, quemConfere.Contains(u.Id), u.Id == usuarioLogadoId))
            .OrderBy(c => c.Nome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Id)
            .ToList();
    }
}

public sealed record ContaResumo(Guid Id, string Nome, string Email, Papel Papel, bool Ativo, bool TambemConfere, bool EhVoce);
