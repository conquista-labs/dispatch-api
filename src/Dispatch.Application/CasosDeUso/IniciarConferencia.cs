using Dispatch.Domain;

namespace Dispatch.Application;

// RF-21: arranca o cronômetro de um protocolo já atribuído ao próprio conferente. O limite de
// simultâneos é hardcoded por ora — mesma pendência do semáforo (seção 8, tabela `config`
// ainda não existe).
public sealed class IniciarConferencia(
    IProtocoloRepository protocolos,
    IRelogio relogio,
    IUnitOfWork unitOfWork)
{
    private const int LimiteDeAtosSimultaneos = 1;

    public async Task<ResultadoIniciarConferencia> ExecutarAsync(
        Guid protocoloId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoIniciarConferencia.NaoEncontrado;
        }

        if (protocolo.Status != StatusProtocolo.Atribuido || protocolo.DonoId != conferente.Id)
        {
            return ResultadoIniciarConferencia.NaoEhSeuOuNaoEstaAtribuido;
        }

        var emConferencia = await protocolos.ObterEmConferenciaPorConferenteAsync(conferente.Id, cancellationToken);
        if (emConferencia.Count >= LimiteDeAtosSimultaneos)
        {
            return ResultadoIniciarConferencia.LimiteDeSimultaneosAtingido;
        }

        protocolo.IniciarConferencia(relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoIniciarConferencia.Sucesso;
    }
}

public enum ResultadoIniciarConferencia
{
    Sucesso,
    NaoEncontrado,
    NaoEhSeuOuNaoEstaAtribuido,
    LimiteDeSimultaneosAtingido
}
