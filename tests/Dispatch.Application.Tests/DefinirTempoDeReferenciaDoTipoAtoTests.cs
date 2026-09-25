using Dispatch.Domain;

namespace Dispatch.Application.Tests;

// RF-34a: o stepper grava o informado (2–240); nulo = "usar histórico".
public class DefinirTempoDeReferenciaDoTipoAtoTests
{
    [Fact]
    public async Task Informado_GravaENulo_Apaga()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = new DefinirTempoDeReferenciaDoTipoAto(new FakeTipoAtoRepository([tipo]), new FakeUnitOfWork());

        Assert.IsType<ResultadoDefinirTempoDeReferencia.Sucesso>(await casoDeUso.ExecutarAsync(tipo.Id, 38));
        Assert.Equal(38, tipo.TempoReferenciaMinutos);

        Assert.IsType<ResultadoDefinirTempoDeReferencia.Sucesso>(await casoDeUso.ExecutarAsync(tipo.Id, null));
        Assert.Null(tipo.TempoReferenciaMinutos);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(241)]
    public async Task ForaDaFaixa_RecusaComMotivoESemMudar(int minutos)
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        tipo.DefinirTempoDeReferencia(20);
        var casoDeUso = new DefinirTempoDeReferenciaDoTipoAto(new FakeTipoAtoRepository([tipo]), new FakeUnitOfWork());

        var invalido = Assert.IsType<ResultadoDefinirTempoDeReferencia.Invalido>(await casoDeUso.ExecutarAsync(tipo.Id, minutos));

        Assert.StartsWith("minutos", invalido.Motivo);
        Assert.Equal(20, tipo.TempoReferenciaMinutos);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var casoDeUso = new DefinirTempoDeReferenciaDoTipoAto(new FakeTipoAtoRepository([]), new FakeUnitOfWork());

        Assert.IsType<ResultadoDefinirTempoDeReferencia.NaoEncontrado>(await casoDeUso.ExecutarAsync(Guid.NewGuid(), 30));
    }
}
