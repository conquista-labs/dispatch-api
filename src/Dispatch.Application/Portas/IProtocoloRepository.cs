using Dispatch.Domain;

namespace Dispatch.Application;

public interface IProtocoloRepository
{
    void Adicionar(Protocolo protocolo);
    Task<Protocolo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Protocolo>> ObterAtribuidosAAsync(Guid conferenteId, CancellationToken cancellationToken);

    // RF-13: loteImportacaoId nulo = todos os protocolos (sem filtrar por lote).
    Task<IReadOnlyCollection<Protocolo>> ObterParaDistribuicaoAsync(Guid? loteImportacaoId, CancellationToken cancellationToken);
}
