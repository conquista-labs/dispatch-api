using Dispatch.Domain;

namespace Dispatch.Application;

// A importação de lote nunca define prioridade alta (o relatório do cartório não tem essa
// coluna) — este é o único caminho real pra marcar um protocolo como urgente hoje, uma decisão
// humana explícita da distribuidora.
public sealed class DefinirPrioridadeDoProtocolo(IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid protocoloId, Prioridade prioridade, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return false;
        }

        protocolo.DefinirPrioridade(prioridade);
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
