using Dispatch.Domain;

namespace Dispatch.Application;

// Pedido do dono ("a pessoa sai pra almoçar, por exemplo") — congela o cronômetro do ciclo em
// andamento sem devolver o ato pra fila: continua ocupando o limite de simultâneos (RF-21), a
// pessoa não pode iniciar outro enquanto este estiver pausado.
public sealed class PausarConferencia(
    IProtocoloRepository protocolos,
    IRelogio relogio,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoPausarConferencia> ExecutarAsync(
        Guid protocoloId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoPausarConferencia.NaoEncontrado;
        }

        if (protocolo.Status != StatusProtocolo.Conferindo || protocolo.DonoId != conferente.Id)
        {
            return ResultadoPausarConferencia.NaoEhSeuOuNaoEstaEmConferencia;
        }

        if (protocolo.PausadoEm is not null)
        {
            return ResultadoPausarConferencia.JaEstaPausado;
        }

        protocolo.Pausar(relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoPausarConferencia.Sucesso;
    }
}

public enum ResultadoPausarConferencia
{
    Sucesso,
    NaoEncontrado,
    NaoEhSeuOuNaoEstaEmConferencia,
    JaEstaPausado
}
