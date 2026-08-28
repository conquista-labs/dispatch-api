using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DevolverAoPoolTests
{
    [Fact]
    public async Task ProtocoloAtribuido_VoltaParaOPool()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var casoDeUso = new DevolverAoPool(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.Equal(ResultadoDevolverAoPool.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
        Assert.Null(protocolo.DonoId);
    }

    [Fact]
    public async Task ProtocoloJaNoPool_Rejeita()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new DevolverAoPool(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.Equal(ResultadoDevolverAoPool.ProtocoloNaoEstaAtribuido, resultado);
    }

    [Fact]
    public async Task ProtocoloInexistente_Rejeita()
    {
        var casoDeUso = new DevolverAoPool(new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.Equal(ResultadoDevolverAoPool.ProtocoloNaoEncontrado, resultado);
    }
}
