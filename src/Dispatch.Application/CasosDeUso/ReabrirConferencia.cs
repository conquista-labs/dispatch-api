using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18a: ação direta da distribuidora no painel de detalhe do protocolo, sem exigir um
// pedido de reabertura explícito do conferente — mesma transição que DecidirPedidoReabertura
// aplica quando aprova um pedido.
public sealed class ReabrirConferencia(IProtocoloRepository protocolos, IRelogio relogio, IUnitOfWork unitOfWork)
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

        protocolo.ReabrirConferencia(relogio.Agora);
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
