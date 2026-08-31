using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24b: "cancelável enquanto pendente" — só o próprio solicitante.
public sealed class CancelarPedidoReabertura(IPedidoReaberturaRepository pedidos, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoCancelarPedidoReabertura> ExecutarAsync(
        Guid pedidoId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var pedido = await pedidos.ObterPorIdAsync(pedidoId, cancellationToken);
        if (pedido is null)
        {
            return new ResultadoCancelarPedidoReabertura.NaoEncontrado();
        }

        if (pedido.SolicitanteId != conferente.Id)
        {
            return new ResultadoCancelarPedidoReabertura.NaoEhSeu();
        }

        if (pedido.Status != StatusPedidoReabertura.Pendente)
        {
            return new ResultadoCancelarPedidoReabertura.NaoEstaPendente();
        }

        pedido.Cancelar();
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoCancelarPedidoReabertura.Sucesso();
    }
}

public abstract record ResultadoCancelarPedidoReabertura
{
    private ResultadoCancelarPedidoReabertura() { }

    public sealed record Sucesso : ResultadoCancelarPedidoReabertura;

    public sealed record NaoEncontrado : ResultadoCancelarPedidoReabertura;

    public sealed record NaoEhSeu : ResultadoCancelarPedidoReabertura;

    public sealed record NaoEstaPendente : ResultadoCancelarPedidoReabertura;
}
