using Dispatch.Domain;

namespace Dispatch.Application.Tests;

// RF-34f (fatia 5 do Dashboard v2): peso decimal 0,50–2,50 em passos de 0,05; fora disso é recusado
// com motivo (não clampado em silêncio, como era com o inteiro).
public class DefinirPesoDeComplexidadeDoTipoAtoTests
{
    [Fact]
    public async Task PesoDecimalValido_Aplica()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = new DefinirPesoDeComplexidadeDoTipoAto(new FakeTipoAtoRepository([tipo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(tipo.Id, 1.35m);

        Assert.IsType<ResultadoDefinirPesoDeComplexidade.Sucesso>(resultado);
        Assert.Equal(1.35m, tipo.PesoComplexidade);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(0.45)]
    [InlineData(1.33)]
    public async Task PesoForaDaFaixaOuDoPasso_RecusaComMotivoESemMudar(double peso)
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = new DefinirPesoDeComplexidadeDoTipoAto(new FakeTipoAtoRepository([tipo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(tipo.Id, (decimal)peso);

        var invalido = Assert.IsType<ResultadoDefinirPesoDeComplexidade.Invalido>(resultado);
        Assert.Contains("peso de complexidade", invalido.Motivo);
        Assert.Equal(1.00m, tipo.PesoComplexidade);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var casoDeUso = new DefinirPesoDeComplexidadeDoTipoAto(new FakeTipoAtoRepository([]), new FakeUnitOfWork());

        Assert.IsType<ResultadoDefinirPesoDeComplexidade.NaoEncontrado>(await casoDeUso.ExecutarAsync(Guid.NewGuid(), 1.25m));
    }
}
