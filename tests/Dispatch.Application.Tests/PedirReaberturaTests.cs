using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class PedirReaberturaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    private static Conferente NovoConferente() => new(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

    private static Protocolo NovoProtocoloConcluido(Conferente dono)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(dono.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow);
        return protocolo;
    }

    private static PedirReabertura NovoCasoDeUso(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<PedidoReabertura> pedidos, out FakePedidoReaberturaRepository repositorioPedidos) =>
        new(new FakeProtocoloRepository(protocolos), repositorioPedidos = new FakePedidoReaberturaRepository(pedidos), new FakeRelogio(Agora), new FakeUnitOfWork());

    [Fact]
    public async Task ProtocoloConcluido_CriaPedidoPendente()
    {
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente);
        var casoDeUso = NovoCasoDeUso([protocolo], [], out var pedidos);

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        var sucesso = Assert.IsType<ResultadoPedirReabertura.Sucesso>(resultado);
        Assert.Equal(1, pedidos.Quantidade);
        var pedido = (await pedidos.ObterPorIdAsync(sucesso.PedidoId, CancellationToken.None))!;
        Assert.Equal(protocolo.Id, pedido.ProtocoloId);
        Assert.Equal(conferente.Id, pedido.SolicitanteId);
        Assert.Equal(StatusPedidoReabertura.Pendente, pedido.Status);
    }

    [Fact]
    public async Task JaExistePedidoPendente_Rejeita()
    {
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente);
        var pedidoExistente = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferente.Id, Agora.AddMinutes(-10));
        var casoDeUso = NovoCasoDeUso([protocolo], [pedidoExistente], out var pedidos);

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.IsType<ResultadoPedirReabertura.JaExistePedidoPendente>(resultado);
        Assert.Equal(1, pedidos.Quantidade);
    }

    [Fact]
    public async Task NaoEhODono_Rejeita()
    {
        var dono = NovoConferente();
        var outroConferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(dono);
        var casoDeUso = NovoCasoDeUso([protocolo], [], out _);

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, outroConferente);

        Assert.IsType<ResultadoPedirReabertura.NaoEhSeu>(resultado);
    }

    [Fact]
    public async Task ProtocoloAindaEmConferencia_StatusInvalido()
    {
        var conferente = NovoConferente();
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        var casoDeUso = NovoCasoDeUso([protocolo], [], out _);

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.IsType<ResultadoPedirReabertura.StatusInvalido>(resultado);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var conferente = NovoConferente();
        var casoDeUso = NovoCasoDeUso([], [], out _);

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente);

        Assert.IsType<ResultadoPedirReabertura.ProtocoloNaoEncontrado>(resultado);
    }
}
