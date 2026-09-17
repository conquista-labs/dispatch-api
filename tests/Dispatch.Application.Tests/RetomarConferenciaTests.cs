using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class RetomarConferenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtocoloPausadoDoConferente_Retoma()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Agora.AddHours(-1));
        protocolo.Pausar(Agora.AddMinutes(-50));
        var casoDeUso = new RetomarConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoRetomarConferencia.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Conferindo, protocolo.Status);
        Assert.Equal(Agora, protocolo.IniciadoEm);
        Assert.Null(protocolo.PausadoEm);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new RetomarConferencia(new FakeProtocoloRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente);

        Assert.Equal(ResultadoRetomarConferencia.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task ProtocoloDeOutroConferente_RetornaNaoEhSeuOuNaoEstaEmConferencia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Agora.AddHours(-1));
        protocolo.Pausar(Agora.AddMinutes(-50));
        var casoDeUso = new RetomarConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoRetomarConferencia.NaoEhSeuOuNaoEstaEmConferencia, resultado);
    }

    [Fact]
    public async Task ProtocoloNaoPausado_RetornaNaoEstaPausado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Agora.AddHours(-1));
        var casoDeUso = new RetomarConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoRetomarConferencia.NaoEstaPausado, resultado);
    }
}
