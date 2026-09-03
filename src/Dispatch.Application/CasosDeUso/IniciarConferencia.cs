using Dispatch.Domain;

namespace Dispatch.Application;

// RF-21: arranca o cronômetro de um protocolo já atribuído ao próprio conferente. O limite de
// simultâneos vem da tabela `config` (seção 8).
public sealed class IniciarConferencia(
    IProtocoloRepository protocolos,
    IConfiguracaoRepository configuracao,
    IRelogio relogio,
    IUnitOfWork unitOfWork)
{
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
        var config = await configuracao.ObterAsync(cancellationToken);
        if (emConferencia.Count >= config.LimiteDeAtosSimultaneos)
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
