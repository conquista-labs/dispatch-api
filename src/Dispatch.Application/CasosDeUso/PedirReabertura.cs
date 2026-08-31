using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24b: fora da janela de correção, o conferente não devolve o ato sozinho — abre um
// pedido pra distribuidora decidir. Não checa a janela de CorrigirResultado aqui de propósito:
// o requisito não proíbe pedir reabertura mesmo dentro da janela (o conferente pode preferir
// isso a corrigir sozinho); a única guarda de exclusividade é "só um pedido pendente por vez".
public sealed class PedirReabertura(
    IProtocoloRepository protocolos, IPedidoReaberturaRepository pedidos, IRelogio relogio, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoPedirReabertura> ExecutarAsync(
        Guid protocoloId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return new ResultadoPedirReabertura.ProtocoloNaoEncontrado();
        }

        if (protocolo.DonoId != conferente.Id)
        {
            return new ResultadoPedirReabertura.NaoEhSeu();
        }

        if (protocolo.Status is not (StatusProtocolo.Aprovado or StatusProtocolo.Reprovado))
        {
            return new ResultadoPedirReabertura.StatusInvalido();
        }

        if (await pedidos.ObterPendentePorProtocoloAsync(protocoloId, cancellationToken) is not null)
        {
            return new ResultadoPedirReabertura.JaExistePedidoPendente();
        }

        var pedido = new PedidoReabertura(Guid.NewGuid(), protocoloId, conferente.Id, relogio.Agora);
        pedidos.Adicionar(pedido);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoPedirReabertura.Sucesso(pedido.Id);
    }
}

public abstract record ResultadoPedirReabertura
{
    private ResultadoPedirReabertura() { }

    public sealed record Sucesso(Guid PedidoId) : ResultadoPedirReabertura;

    public sealed record ProtocoloNaoEncontrado : ResultadoPedirReabertura;

    public sealed record NaoEhSeu : ResultadoPedirReabertura;

    public sealed record StatusInvalido : ResultadoPedirReabertura;

    public sealed record JaExistePedidoPendente : ResultadoPedirReabertura;
}
