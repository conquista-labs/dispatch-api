using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ConcluirConferenciaTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 8, 27, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Fim = Inicio.AddMinutes(15);

    [Fact]
    public async Task Aprovado_ConcluiEGravaDuracao()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Inicio);
        var casoDeUso = new ConcluirConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeRelogio(Fim), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente, aprovado: true);

        Assert.Equal(ResultadoConcluirConferencia.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Aprovado, protocolo.Status);
        Assert.Equal(TimeSpan.FromMinutes(15), protocolo.Duracao);
    }

    [Fact]
    public async Task Reprovado_Conclui()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Inicio);
        var casoDeUso = new ConcluirConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeRelogio(Fim), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente, aprovado: false);

        Assert.Equal(ResultadoConcluirConferencia.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Reprovado, protocolo.Status);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new ConcluirConferencia(
            new FakeProtocoloRepository([]), new FakeRelogio(Fim), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente, aprovado: true);

        Assert.Equal(ResultadoConcluirConferencia.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task ProtocoloAindaNaoIniciado_RetornaNaoEhSeuOuNaoEstaEmConferencia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        var casoDeUso = new ConcluirConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeRelogio(Fim), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente, aprovado: true);

        Assert.Equal(ResultadoConcluirConferencia.NaoEhSeuOuNaoEstaEmConferencia, resultado);
    }

    [Fact]
    public async Task ProtocoloDeOutroConferente_RetornaNaoEhSeuOuNaoEstaEmConferencia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(Inicio);
        var casoDeUso = new ConcluirConferencia(
            new FakeProtocoloRepository([protocolo]), new FakeRelogio(Fim), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente, aprovado: true);

        Assert.Equal(ResultadoConcluirConferencia.NaoEhSeuOuNaoEstaEmConferencia, resultado);
    }
}
