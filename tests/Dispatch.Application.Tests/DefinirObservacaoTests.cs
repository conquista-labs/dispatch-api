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

        Assert.True(resultado);
        Assert.Equal("cliente pediu pra aguardar", protocolo.Observacao);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaFalse()
    {
        var casoDeUso = new DefinirObservacao(new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), "x");

        Assert.False(resultado);
    }
}
