using Dispatch.Domain;

namespace Dispatch.Application;

// Pass-through fino, mesmo molde de ObterUsuarioAtual — a Api nunca injeta repositório direto,
// mesmo pra leitura trivial. Consumido tanto por GET /config quanto pelos endpoints que
// precisam das faixas do semáforo pra montar ProtocoloResumo/DetalheProtocoloResponse.
public sealed class ObterConfiguracao(IConfiguracaoRepository configuracao)
{
    public Task<Configuracao> ExecutarAsync(CancellationToken cancellationToken = default) =>
        configuracao.ObterAsync(cancellationToken);
}
