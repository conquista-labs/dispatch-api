using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ListarPedidosReaberturaPendentesTests
{
    private static ListarPedidosReaberturaPendentes NovoCasoDeUso(
        IReadOnlyCollection<PedidoReabertura> pedidos,
        IReadOnlyCollection<Protocolo> protocolos,
        IReadOnlyCollection<Conferente> conferentes,
        IReadOnlyCollection<Usuario> usuarios) =>
        new(
            new FakePedidoReaberturaRepository(pedidos),
            new FakeProtocoloRepository(protocolos),
            new FakeConferenteRepository(conferentes),
            new FakeUsuarioRepository(usuarios));

    [Fact]
    public async Task PedidoPendente_JuntaProtocoloENomeDoSolicitante()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Ana Conferente", "ana@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "262001", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow);
        var pedido = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferente.Id, DateTimeOffset.UtcNow);
        var casoDeUso = NovoCasoDeUso([pedido], [protocolo], [conferente], [usuario]);

        var lista = await casoDeUso.ExecutarAsync();

        var item = Assert.Single(lista);
        Assert.Equal(pedido.Id, item.PedidoId);
        Assert.Equal(protocolo.Id, item.ProtocoloId);
        Assert.Equal("262001", item.ProtocoloNumero);
        Assert.Equal("Ana Conferente", item.NomeSolicitante);
        Assert.Equal(StatusProtocolo.Aprovado, item.StatusAtual);
    }

    [Fact]
    public async Task PedidoJaDecidido_NaoAparece()
    {
        var conferenteId = Guid.NewGuid();
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        var pedido = new PedidoReabertura(Guid.NewGuid(), protocolo.Id, conferenteId, DateTimeOffset.UtcNow);
        pedido.Negar(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var casoDeUso = NovoCasoDeUso([pedido], [protocolo], [], []);

        var lista = await casoDeUso.ExecutarAsync();

        Assert.Empty(lista);
    }

    [Fact]
    public async Task SemPedidoNenhum_ListaVazia()
    {
        var casoDeUso = NovoCasoDeUso([], [], [], []);

        var lista = await casoDeUso.ExecutarAsync();

        Assert.Empty(lista);
    }
}
