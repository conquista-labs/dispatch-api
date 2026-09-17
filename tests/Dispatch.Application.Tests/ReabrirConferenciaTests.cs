using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ReabrirConferenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtocoloConcluido_ReabreEDevolveAoDono_QuandoEleAindaEstaNaEscala()
    {
        var dono = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(dono.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow.AddHours(-1));
        var casoDeUso = new ReabrirConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([dono]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.IsType<ResultadoReabrirConferencia.Sucesso>(resultado);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(dono.Id, protocolo.DonoId);
        Assert.Equal(Agora, protocolo.ReabertoEm);
    }

    // Achado pensando no caso real (protocolo 263605): reabertura "sempre volta pro mesmo
    // conferente, exceto se a pessoa estiver fora da escala" — nesse caso vai pro pool (mesmo
    // raciocínio de MarcarPresenca/RF-27), não fica presa em Atribuído pra alguém ausente.
    [Fact]
    public async Task ProtocoloConcluido_VaiParaOPool_QuandoODonoSaiuDaEscala()
    {
        var dono = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: false, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(dono.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow.AddHours(-1));
        var casoDeUso = new ReabrirConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([dono]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.IsType<ResultadoReabrirConferencia.Sucesso>(resultado);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
        Assert.Null(protocolo.DonoId);
        Assert.Equal(Agora, protocolo.ReabertoEm);
    }

    [Fact]
    public async Task ProtocoloNoPool_StatusInvalido()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new ReabrirConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.IsType<ResultadoReabrirConferencia.StatusInvalido>(resultado);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var casoDeUso = new ReabrirConferencia(
            new FakeProtocoloRepository([]), new FakeConferenteRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.IsType<ResultadoReabrirConferencia.NaoEncontrado>(resultado);
    }
}
