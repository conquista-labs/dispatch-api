using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DefinirPesoDeComplexidadeDoTipoAtoTests
{
    [Fact]
    public async Task PesoValido_Aplica()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var tiposAto = new FakeTipoAtoRepository([tipo]);
        var casoDeUso = new DefinirPesoDeComplexidadeDoTipoAto(tiposAto, new FakeUnitOfWork());

        var encontrado = await casoDeUso.ExecutarAsync(tipo.Id, 3);

        Assert.True(encontrado);
        Assert.Equal(3, tipo.PesoComplexidade);
    }

    [Fact]
    public async Task PesoZeroOuNegativo_ClampaParaUm()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var tiposAto = new FakeTipoAtoRepository([tipo]);
        var casoDeUso = new DefinirPesoDeComplexidadeDoTipoAto(tiposAto, new FakeUnitOfWork());

        await casoDeUso.ExecutarAsync(tipo.Id, -5);

        Assert.Equal(1, tipo.PesoComplexidade);
    }

    [Fact]
    public async Task IdInexistente_DevolveFalso()
    {
        var tiposAto = new FakeTipoAtoRepository([]);
        var casoDeUso = new DefinirPesoDeComplexidadeDoTipoAto(tiposAto, new FakeUnitOfWork());

        Assert.False(await casoDeUso.ExecutarAsync(Guid.NewGuid(), 2));
    }
}
