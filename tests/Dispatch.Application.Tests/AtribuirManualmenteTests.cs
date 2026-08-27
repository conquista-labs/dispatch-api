using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class AtribuirManualmenteTests
{
    [Fact]
    public async Task ProtocoloEmExcecao_AtribuiComSucesso()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.MarcarExcecao("ninguém com alçada");
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new AtribuirManualmente(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente.Id);

        Assert.Equal(ResultadoAtribuirManualmente.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(conferente.Id, protocolo.DonoId);
    }

    [Fact]
    public async Task ProtocoloNaoEstaEmExcecao_Rejeita()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new AtribuirManualmente(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente.Id);

        Assert.Equal(ResultadoAtribuirManualmente.ProtocoloNaoEstaEmExcecao, resultado);
    }

    [Fact]
    public async Task ConferenteInexistente_Rejeita()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.MarcarExcecao("ninguém com alçada");
        var casoDeUso = new AtribuirManualmente(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, Guid.NewGuid());

        Assert.Equal(ResultadoAtribuirManualmente.ConferenteNaoEncontrado, resultado);
    }

    [Fact]
    public async Task ProtocoloInexistente_Rejeita()
    {
        var casoDeUso = new AtribuirManualmente(
            new FakeProtocoloRepository([]), new FakeConferenteRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ResultadoAtribuirManualmente.ProtocoloNaoEncontrado, resultado);
    }
}
