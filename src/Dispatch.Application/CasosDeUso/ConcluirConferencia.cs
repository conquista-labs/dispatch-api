using Dispatch.Domain;

namespace Dispatch.Application;

// RF-22: encerra o ato — aprovado ou não aprovado, o protocolo sai da fila "em conferência"
// e grava a duração via Protocolo.ConcluidoEm/Duracao.
public sealed class ConcluirConferencia(
    IProtocoloRepository protocolos,
    IRelogio relogio,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoConcluirConferencia> ExecutarAsync(
        Guid protocoloId, Conferente conferente, bool aprovado, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoConcluirConferencia.NaoEncontrado;
        }

        if (protocolo.Status != StatusProtocolo.Conferindo || protocolo.DonoId != conferente.Id)
        {
            return ResultadoConcluirConferencia.NaoEhSeuOuNaoEstaEmConferencia;
        }

        if (aprovado)
        {
            protocolo.Aprovar(relogio.Agora);
        }
        else
        {
            protocolo.Reprovar(relogio.Agora);
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoConcluirConferencia.Sucesso;
    }
}

public enum ResultadoConcluirConferencia
{
    Sucesso,
    NaoEncontrado,
    NaoEhSeuOuNaoEstaEmConferencia
}
