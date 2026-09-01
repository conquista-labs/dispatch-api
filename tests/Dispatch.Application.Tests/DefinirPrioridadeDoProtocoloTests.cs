using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DefinirPrioridadeDoProtocoloTests
{
    [Fact]
    public async Task ProtocoloExistente_MarcaComoUrgente()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new DefinirPrioridadeDoProtocolo(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, Prioridade.Alta);

        Assert.True(resultado);
        Assert.Equal(Prioridade.Alta, protocolo.Prioridade);
        Assert.True(protocolo.Urgente);
    }

    [Fact]
    public async Task ProtocoloExistente_RemoveUrgencia()
    {
        var protocolo = new Protocolo(
            Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow, Prioridade.Alta);
        var casoDeUso = new DefinirPrioridadeDoProtocolo(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, Prioridade.Normal);

        Assert.True(resultado);
        Assert.Equal(Prioridade.Normal, protocolo.Prioridade);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaFalse()
    {
        var casoDeUso = new DefinirPrioridadeDoProtocolo(new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), Prioridade.Alta);

        Assert.False(resultado);
    }
}
