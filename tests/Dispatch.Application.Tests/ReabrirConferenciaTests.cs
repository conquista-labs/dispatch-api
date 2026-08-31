using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ReabrirConferenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtocoloConcluido_Reabre()
    {
        var donoId = Guid.NewGuid();
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(donoId, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow.AddHours(-1));
        var casoDeUso = new ReabrirConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.IsType<ResultadoReabrirConferencia.Sucesso>(resultado);
        Assert.Equal(StatusProtocolo.Conferindo, protocolo.Status);
        Assert.Equal(donoId, protocolo.DonoId);
        Assert.Equal(Agora, protocolo.ReabertoEm);
    }

    [Fact]
    public async Task ProtocoloNoPool_StatusInvalido()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new ReabrirConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.IsType<ResultadoReabrirConferencia.StatusInvalido>(resultado);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var casoDeUso = new ReabrirConferencia(new FakeProtocoloRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.IsType<ResultadoReabrirConferencia.NaoEncontrado>(resultado);
    }
}
