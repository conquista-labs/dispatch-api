using Dispatch.Domain;

namespace Dispatch.Application;

// RF-17: ação de resolução da fila de exceções — atribuir na mão, sem passar pelo motor
// (o motor já disse que não sabe resolver sozinho, é exatamente por isso que virou exceção).
// Só aplica a protocolos que estão de fato em exceção — não é um "reatribuir" genérico.
public sealed class AtribuirManualmente(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoAtribuirManualmente> ExecutarAsync(
        Guid protocoloId, Guid conferenteId, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoAtribuirManualmente.ProtocoloNaoEncontrado;
        }

        if (protocolo.Status != StatusProtocolo.Excecao)
        {
            return ResultadoAtribuirManualmente.ProtocoloNaoEstaEmExcecao;
        }

        var conferente = await conferentes.ObterPorIdAsync(conferenteId, cancellationToken);
        if (conferente is null)
        {
            return ResultadoAtribuirManualmente.ConferenteNaoEncontrado;
        }

        protocolo.AtribuirA(conferente.Id);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoAtribuirManualmente.Sucesso;
    }
}

public enum ResultadoAtribuirManualmente
{
    Sucesso,
    ProtocoloNaoEncontrado,
    ProtocoloNaoEstaEmExcecao,
    ConferenteNaoEncontrado
}
