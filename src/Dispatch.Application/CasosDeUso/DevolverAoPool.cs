using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18a: uma das ações do painel de detalhe — desfaz uma atribuição específica, devolvendo o
// protocolo pro pool. Diferente de RedistribuirPool (RF-16, reaplica o motor a tudo sem dono):
// aqui é uma decisão pontual da distribuidora, não uma reavaliação de alçada.
public sealed class DevolverAoPool(IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoDevolverAoPool> ExecutarAsync(Guid protocoloId, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoDevolverAoPool.ProtocoloNaoEncontrado;
        }

        if (protocolo.Status != StatusProtocolo.Atribuido)
        {
            return ResultadoDevolverAoPool.ProtocoloNaoEstaAtribuido;
        }

        protocolo.EnviarParaPool();
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoDevolverAoPool.Sucesso;
    }
}

public enum ResultadoDevolverAoPool
{
    Sucesso,
    ProtocoloNaoEncontrado,
    ProtocoloNaoEstaAtribuido
}
