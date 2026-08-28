using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DefinirObservacaoTests
{
    [Fact]
    public async Task ProtocoloExistente_DefineObservacao()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new DefinirObservacao(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, "cliente pediu pra aguardar");

        Assert.Equal(ResultadoDefinirObservacao.Sucesso, resultado);
        Assert.Equal("cliente pediu pra aguardar", protocolo.Observacao);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var casoDeUso = new DefinirObservacao(new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), "x");

        Assert.Equal(ResultadoDefinirObservacao.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task ConferenteRestrito_NaoEhDono_RetornaNaoEhSeu()
    {
        var dono = Guid.NewGuid();
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(dono, DateTimeOffset.UtcNow);
        var casoDeUso = new DefinirObservacao(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, "x", conferenteRestritoId: Guid.NewGuid());

        Assert.Equal(ResultadoDefinirObservacao.NaoEhSeu, resultado);
        Assert.Null(protocolo.Observacao);
    }

    [Fact]
    public async Task ConferenteRestrito_EhDono_DefineObservacao()
    {
        var dono = Guid.NewGuid();
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(dono, DateTimeOffset.UtcNow);
        var casoDeUso = new DefinirObservacao(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, "concluído com ressalva", conferenteRestritoId: dono);

        Assert.Equal(ResultadoDefinirObservacao.Sucesso, resultado);
        Assert.Equal("concluído com ressalva", protocolo.Observacao);
    }
}
