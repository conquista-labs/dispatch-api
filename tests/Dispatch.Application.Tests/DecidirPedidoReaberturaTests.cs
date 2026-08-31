using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DecidirPedidoReaberturaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    private static Conferente NovoConferente() => new(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

    private static Protocolo NovoProtocoloConcluido(Conferente dono)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(dono.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow.AddHours(-1));
        return protocolo;
    }

    [Fact]
    public async Task Aprovar_ReabreOProtocoloComMesmoDono()
    {
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente);
        var pedido = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferente.Id, Agora.AddMinutes(-5));
        var distribuidoraId = Guid.NewGuid();
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, aprovar: true, distribuidoraId);

        Assert.IsType<ResultadoDecidirPedidoReabertura.Sucesso>(resultado);
        Assert.Equal(StatusPedidoReabertura.Aprovado, pedido.Status);
        Assert.Equal(distribuidoraId, pedido.DecididoPorId);
        Assert.Equal(StatusProtocolo.Conferindo, protocolo.Status);
        Assert.Equal(conferente.Id, protocolo.DonoId);
        Assert.Equal(Agora, protocolo.IniciadoEm);
        Assert.Equal(Agora, protocolo.ReabertoEm);
    }

    [Fact]
    public async Task Negar_SoMarcaOPedido_ProtocoloNaoMuda()
    {
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente);
        var pedido = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferente.Id, Agora.AddMinutes(-5));
        var distribuidoraId = Guid.NewGuid();
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, aprovar: false, distribuidoraId);

        Assert.IsType<ResultadoDecidirPedidoReabertura.Sucesso>(resultado);
        Assert.Equal(StatusPedidoReabertura.Negado, pedido.Status);
        Assert.Equal(StatusProtocolo.Aprovado, protocolo.Status);
    }

    [Fact]
    public async Task JaDecidido_NaoEstaPendente()
    {
        var pedido = new PedidoReabertura(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Agora.AddMinutes(-5));
        pedido.Cancelar();
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, aprovar: true, Guid.NewGuid());

        Assert.IsType<ResultadoDecidirPedidoReabertura.NaoEstaPendente>(resultado);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([]), new FakeProtocoloRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), aprovar: true, Guid.NewGuid());

        Assert.IsType<ResultadoDecidirPedidoReabertura.NaoEncontrado>(resultado);
    }
}
