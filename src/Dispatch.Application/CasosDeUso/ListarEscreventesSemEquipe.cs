using Dispatch.Domain;

namespace Dispatch.Application;

// RF-37 (a parte de listar — a de alocar é MoverEscreventeParaEquipe).
public sealed class ListarEscreventesSemEquipe(IEscreventeRepository escreventes)
{
    public async Task<IReadOnlyList<Escrevente>> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var todos = await escreventes.ObterTodosAsync(cancellationToken);
        return todos.Where(e => e.EquipeId is null).ToList();
    }
}
