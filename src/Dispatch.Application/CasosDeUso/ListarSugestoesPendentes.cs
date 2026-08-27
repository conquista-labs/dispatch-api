using Dispatch.Domain;

namespace Dispatch.Application;

// RF-39: fila de propostas — só as pendentes, cada uma já carrega evidência e ocorrências.
public sealed class ListarSugestoesPendentes(ISugestaoRepository sugestoes)
{
    public Task<IReadOnlyCollection<Sugestao>> ExecutarAsync(CancellationToken cancellationToken = default) =>
        sugestoes.ObterPendentesAsync(cancellationToken);
}
