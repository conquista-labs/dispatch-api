using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24c: a distribuidora decide um pedido pendente — aprovar reabre o protocolo de verdade
// (mesmo dono, cronômetro do zero); negar só marca o pedido, o protocolo não muda.
public sealed class DecidirPedidoReabertura(
    IPedidoReaberturaRepository pedidos, IProtocoloRepository protocolos, IRelogio relogio, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoDecidirPedidoReabertura> ExecutarAsync(
        Guid pedidoId, bool aprovar, Guid decididoPorId, CancellationToken cancellationToken = default)
    {
        var pedido = await pedidos.ObterPorIdAsync(pedidoId, cancellationToken);
        if (pedido is null)
        {
            return new ResultadoDecidirPedidoReabertura.NaoEncontrado();
        }

        if (pedido.Status != StatusPedidoReabertura.Pendente)
        {
            return new ResultadoDecidirPedidoReabertura.NaoEstaPendente();
        }

        var agora = relogio.Agora;
        if (aprovar)
        {
            // Protocolo sempre existe aqui — pedido não é criado sem protocolo válido
            // (PedirReabertura já valida isso), e protocolos não são apagados.
            var protocolo = await protocolos.ObterPorIdAsync(pedido.ProtocoloId, cancellationToken);
            protocolo!.ReabrirConferencia(agora);
            pedido.Aprovar(decididoPorId, agora);
        }
        else
        {
            pedido.Negar(decididoPorId, agora);
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoDecidirPedidoReabertura.Sucesso();
    }
}

public abstract record ResultadoDecidirPedidoReabertura
{
    private ResultadoDecidirPedidoReabertura() { }

    public sealed record Sucesso : ResultadoDecidirPedidoReabertura;

    public sealed record NaoEncontrado : ResultadoDecidirPedidoReabertura;

    public sealed record NaoEstaPendente : ResultadoDecidirPedidoReabertura;
}
