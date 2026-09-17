using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18a: ação direta da distribuidora no painel de detalhe do protocolo, sem exigir um
// pedido de reabertura explícito do conferente — mesma transição que DecidirPedidoReabertura
// aplica quando aprova um pedido.
public sealed class ReabrirConferencia(
    IProtocoloRepository protocolos, IConferenteRepository conferentes, IRelogio relogio, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoReabrirConferencia> ExecutarAsync(Guid protocoloId, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return new ResultadoReabrirConferencia.NaoEncontrado();
        }

        if (protocolo.Status is not (StatusProtocolo.Aprovado or StatusProtocolo.Reprovado))
        {
            return new ResultadoReabrirConferencia.StatusInvalido();
        }

        // RF-27: quem já saiu da escala não recebe o ato de volta — mesmo raciocínio de
        // MarcarPresenca(ausente) (protocolos atribuídos voltam ao pool), só que checado na
        // hora da reabertura em vez de esperar alguém marcar ausência depois. Sem isso, o ato
        // ficava preso em Atribuído pra alguém que ninguém mais está olhando (achado
        // pensando no caso real: reabertura "sempre volta pro mesmo conferente, exceto se a
        // pessoa estiver fora da escala").
        var donoAnterior = protocolo.DonoId is { } donoId ? await conferentes.ObterPorIdAsync(donoId, cancellationToken) : null;
        protocolo.ReabrirConferencia(relogio.Agora);
        if (donoAnterior is null || !donoAnterior.NaEscala)
        {
            protocolo.EnviarParaPool();
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoReabrirConferencia.Sucesso();
    }
}

public abstract record ResultadoReabrirConferencia
{
    private ResultadoReabrirConferencia() { }

    public sealed record Sucesso : ResultadoReabrirConferencia;

    public sealed record NaoEncontrado : ResultadoReabrirConferencia;

    public sealed record StatusInvalido : ResultadoReabrirConferencia;
}
