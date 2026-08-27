using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DescartarExcecaoTests
{
    [Fact]
    public async Task ProtocoloEmExcecao_EhDescartado()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.MarcarExcecao("tipo desconhecido");
        var casoDeUso = new DescartarExcecao(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.True(resultado);
        Assert.Equal(StatusProtocolo.Descartado, protocolo.Status);
        // Motivo original fica registrado — é o que explica por que foi descartado.
        Assert.Equal("tipo desconhecido", protocolo.MotivoExcecao);
    }

    [Fact]
    public async Task ProtocoloNaoEstaEmExcecao_Rejeita()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new DescartarExcecao(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.False(resultado);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
    }

    [Fact]
    public async Task ProtocoloInexistente_Rejeita()
    {
        var casoDeUso = new DescartarExcecao(new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.False(resultado);
    }
}
