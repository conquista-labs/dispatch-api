namespace Dispatch.Application;

// RF-15/RF-23.
public sealed class DefinirObservacao(IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid protocoloId, string? observacao, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return false;
        }

        protocolo.DefinirObservacao(observacao);
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
