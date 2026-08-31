using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CancelarPedidoReaberturaTests
{
    private static Conferente NovoConferente() => new(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

    [Fact]
    public async Task Pendente_SolicitanteCancela()
    {
        var conferente = NovoConferente();
        var pedido = new PedidoReabertura(Guid.NewGuid(), Guid.NewGuid(), conferente.Id, DateTimeOffset.UtcNow);
        var casoDeUso = new CancelarPedidoReabertura(new FakePedidoReaberturaRepository([pedido]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, conferente);

        Assert.IsType<ResultadoCancelarPedidoReabertura.Sucesso>(resultado);
        Assert.Equal(StatusPedidoReabertura.Cancelado, pedido.Status);
    }

    [Fact]
    public async Task NaoEhOSolicitante_Rejeita()
    {
        var solicitante = NovoConferente();
        var outroConferente = NovoConferente();
        var pedido = new PedidoReabertura(Guid.NewGuid(), Guid.NewGuid(), solicitante.Id, DateTimeOffset.UtcNow);
        var casoDeUso = new CancelarPedidoReabertura(new FakePedidoReaberturaRepository([pedido]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, outroConferente);

        Assert.IsType<ResultadoCancelarPedidoReabertura.NaoEhSeu>(resultado);
        Assert.Equal(StatusPedidoReabertura.Pendente, pedido.Status);
    }

    [Fact]
    public async Task JaDecidido_NaoEstaPendente()
    {
        var conferente = NovoConferente();
        var pedido = new PedidoReabertura(Guid.NewGuid(), Guid.NewGuid(), conferente.Id, DateTimeOffset.UtcNow);
        pedido.Negar(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var casoDeUso = new CancelarPedidoReabertura(new FakePedidoReaberturaRepository([pedido]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, conferente);

        Assert.IsType<ResultadoCancelarPedidoReabertura.NaoEstaPendente>(resultado);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var conferente = NovoConferente();
        var casoDeUso = new CancelarPedidoReabertura(new FakePedidoReaberturaRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente);

        Assert.IsType<ResultadoCancelarPedidoReabertura.NaoEncontrado>(resultado);
    }
}
