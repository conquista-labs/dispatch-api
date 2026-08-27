using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class IniciarConferenciaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtocoloAtribuidoAoConferente_Inicia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id);
        var casoDeUso = new IniciarConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoIniciarConferencia.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Conferindo, protocolo.Status);
        Assert.Equal(Agora, protocolo.IniciadoEm);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new IniciarConferencia(
            new FakeProtocoloRepository([]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente);

        Assert.Equal(ResultadoIniciarConferencia.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task ProtocoloDeOutroConferente_RetornaNaoEhSeu()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid());
        var casoDeUso = new IniciarConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoIniciarConferencia.NaoEhSeuOuNaoEstaAtribuido, resultado);
    }

    [Fact]
    public async Task ProtocoloAindaNoPool_RetornaNaoEhSeuOuNaoEstaAtribuido()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new IniciarConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoIniciarConferencia.NaoEhSeuOuNaoEstaAtribuido, resultado);
    }

    [Fact]
    public async Task JaNoLimiteDeSimultaneos_RetornaLimiteAtingido()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

        var jaEmConferencia = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        jaEmConferencia.AtribuirA(conferente.Id);
        jaEmConferencia.IniciarConferencia(Agora);

        var novo = new Protocolo(Guid.NewGuid(), "2", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        novo.AtribuirA(conferente.Id);

        var casoDeUso = new IniciarConferencia(
            new FakeProtocoloRepository([jaEmConferencia, novo]), new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(novo.Id, conferente);

        Assert.Equal(ResultadoIniciarConferencia.LimiteDeSimultaneosAtingido, resultado);
        Assert.Equal(StatusProtocolo.Atribuido, novo.Status);
    }
}
