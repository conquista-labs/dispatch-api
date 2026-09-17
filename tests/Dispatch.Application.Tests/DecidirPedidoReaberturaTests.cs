using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DecidirPedidoReaberturaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    private static Conferente NovoConferente(bool naEscala = true) => new(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala, cargaAtual: 0);

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
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]),
            new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, aprovar: true, distribuidoraId);

        Assert.IsType<ResultadoDecidirPedidoReabertura.Sucesso>(resultado);
        Assert.Equal(StatusPedidoReabertura.Aprovado, pedido.Status);
        Assert.Equal(distribuidoraId, pedido.DecididoPorId);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(conferente.Id, protocolo.DonoId);
        Assert.Null(protocolo.IniciadoEm);
        Assert.Equal(Agora, protocolo.ReabertoEm);
    }

    // Achado pensando no caso real (protocolo 263605): se o conferente que pediu a reabertura
    // já saiu da escala até a distribuidora decidir, o protocolo não pode ficar preso em
    // Atribuído pra ele — vai pro pool (mesmo raciocínio de MarcarPresenca/RF-27).
    [Fact]
    public async Task Aprovar_VaiParaOPool_QuandoOSolicitanteJaSaiuDaEscala()
    {
        var conferente = NovoConferente(naEscala: false);
        var protocolo = NovoProtocoloConcluido(conferente);
        var pedido = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferente.Id, Agora.AddMinutes(-5));
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]),
            new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, aprovar: true, Guid.NewGuid());

        Assert.IsType<ResultadoDecidirPedidoReabertura.Sucesso>(resultado);
        Assert.Equal(StatusPedidoReabertura.Aprovado, pedido.Status);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
        Assert.Null(protocolo.DonoId);
    }

    [Fact]
    public async Task Negar_SoMarcaOPedido_ProtocoloNaoMuda()
    {
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente);
        var pedido = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferente.Id, Agora.AddMinutes(-5));
        var distribuidoraId = Guid.NewGuid();
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]),
            new FakeRelogio(Agora), new FakeUnitOfWork());

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
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([]), new FakeConferenteRepository([]),
            new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, aprovar: true, Guid.NewGuid());

        Assert.IsType<ResultadoDecidirPedidoReabertura.NaoEstaPendente>(resultado);
    }

    [Fact]
    public async Task Aprovar_ProtocoloFoiExcluidoDepoisDoPedido_StatusInvalido()
    {
        // Cenário do bug real: pedido pendente criado, protocolo excluído (soft-delete) nesse
        // meio-tempo, distribuidora aprova o pedido velho sem saber da exclusão — não pode
        // forçar o protocolo de volta pra Conferindo por baixo do RestaurarProtocolo.
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente);
        var pedido = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferente.Id, Agora.AddMinutes(-5));
        protocolo.Excluir();
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([pedido]), new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]),
            new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(pedido.Id, aprovar: true, Guid.NewGuid());

        Assert.IsType<ResultadoDecidirPedidoReabertura.StatusInvalido>(resultado);
        Assert.Equal(StatusPedidoReabertura.Pendente, pedido.Status);
        Assert.Equal(StatusProtocolo.Excluido, protocolo.Status);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var casoDeUso = new DecidirPedidoReabertura(
            new FakePedidoReaberturaRepository([]), new FakeProtocoloRepository([]), new FakeConferenteRepository([]),
            new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), aprovar: true, Guid.NewGuid());

        Assert.IsType<ResultadoDecidirPedidoReabertura.NaoEncontrado>(resultado);
    }
}
