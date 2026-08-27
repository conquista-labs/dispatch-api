using Dispatch.Domain;

namespace Dispatch.Application;

// RF-17: a outra ação da fila de exceções — descartar em vez de resolver.
public sealed class DescartarExcecao(IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid protocoloId, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null || protocolo.Status != StatusProtocolo.Excecao)
        {
            return false;
        }

        protocolo.Descartar();
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
