using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18j: "desfazer" a exclusão dentro de alguns segundos — a janela em si é responsabilidade
// do front (timer do toast); aqui só valida que o protocolo existe e está excluído.
public sealed class RestaurarProtocolo(IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(id, cancellationToken);
        if (protocolo is null || protocolo.Status != StatusProtocolo.Excluido)
        {
            return false;
        }

        protocolo.Restaurar();
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
