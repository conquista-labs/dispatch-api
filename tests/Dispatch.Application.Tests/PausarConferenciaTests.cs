using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class PausarConferenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtocoloEmConferenciaDoConferente_Pausa()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Agora.AddMinutes(-10));
        var casoDeUso = new PausarConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoPausarConferencia.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Conferindo, protocolo.Status);
        Assert.Null(protocolo.IniciadoEm);
        Assert.Equal(Agora, protocolo.PausadoEm);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new PausarConferencia(new FakeProtocoloRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente);

        Assert.Equal(ResultadoPausarConferencia.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task ProtocoloAindaAtribuidoNaoIniciado_RetornaNaoEhSeuOuNaoEstaEmConferencia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        var casoDeUso = new PausarConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoPausarConferencia.NaoEhSeuOuNaoEstaEmConferencia, resultado);
    }

    [Fact]
    public async Task ProtocoloDeOutroConferente_RetornaNaoEhSeuOuNaoEstaEmConferencia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Agora.AddMinutes(-10));
        var casoDeUso = new PausarConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoPausarConferencia.NaoEhSeuOuNaoEstaEmConferencia, resultado);
    }

    [Fact]
    public async Task ProtocoloJaPausado_RetornaJaEstaPausado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Agora.AddMinutes(-10));
        protocolo.Pausar(Agora.AddMinutes(-5));
        var casoDeUso = new PausarConferencia(new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoPausarConferencia.JaEstaPausado, resultado);
    }
}
