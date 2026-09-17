using Dispatch.Domain;

namespace Dispatch.Application;

// Contraparte de PausarConferencia — volta a contar o tempo a partir de agora, abrindo um ciclo
// novo (o pedaço pausado nunca entra em nenhum ciclo, nem no anterior nem no novo).
public sealed class RetomarConferencia(
    IProtocoloRepository protocolos,
    IRelogio relogio,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoRetomarConferencia> ExecutarAsync(
        Guid protocoloId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoRetomarConferencia.NaoEncontrado;
        }

        if (protocolo.Status != StatusProtocolo.Conferindo || protocolo.DonoId != conferente.Id)
        {
            return ResultadoRetomarConferencia.NaoEhSeuOuNaoEstaEmConferencia;
        }

        if (protocolo.PausadoEm is null)
        {
            return ResultadoRetomarConferencia.NaoEstaPausado;
        }

        protocolo.Retomar(relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoRetomarConferencia.Sucesso;
    }
}

public enum ResultadoRetomarConferencia
{
    Sucesso,
    NaoEncontrado,
    NaoEhSeuOuNaoEstaEmConferencia,
    NaoEstaPausado
}
