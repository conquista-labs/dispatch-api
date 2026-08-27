using Dispatch.Domain;

namespace Dispatch.Application;

// RF-41: histórico do que foi aprendido ou descartado, com o efeito de cada decisão.
public sealed class ListarHistoricoSugestoes(ISugestaoRepository sugestoes)
{
    public Task<IReadOnlyCollection<Sugestao>> ExecutarAsync(CancellationToken cancellationToken = default) =>
        sugestoes.ObterHistoricoAsync(cancellationToken);
}
