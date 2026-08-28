using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class RenomearTipoAtoTests
{
    [Fact]
    public async Task NomeNovo_NormalizaERenomeia()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Venda e Compra");
        var tiposAto = new FakeTipoAtoRepository([tipo]);
        var casoDeUso = new RenomearTipoAto(tiposAto, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(tipo.Id, "INVENTÁRIO");

        Assert.IsType<ResultadoRenomearTipoAto.Sucesso>(resultado);
        Assert.Equal("Inventário", tipo.Nome);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var tiposAto = new FakeTipoAtoRepository([]);
        var casoDeUso = new RenomearTipoAto(tiposAto, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), "Inventário");

        Assert.IsType<ResultadoRenomearTipoAto.NaoEncontrado>(resultado);
    }

    [Fact]
    public async Task NomeJaUsadoPorOutroTipo_JaExiste()
    {
        var alvo = new TipoAto(Guid.NewGuid(), "Venda e Compra");
        var outro = new TipoAto(Guid.NewGuid(), "Inventário");
        var tiposAto = new FakeTipoAtoRepository([alvo, outro]);
        var casoDeUso = new RenomearTipoAto(tiposAto, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(alvo.Id, "INVENTÁRIO");

        Assert.IsType<ResultadoRenomearTipoAto.JaExiste>(resultado);
        Assert.Equal("Venda e Compra", alvo.Nome);
    }

    [Fact]
    public async Task RenomearParaOProprioNomeMesmoComCaixaDiferente_NaoConflitaConsigoMesmo()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Venda e Compra");
        var tiposAto = new FakeTipoAtoRepository([tipo]);
        var casoDeUso = new RenomearTipoAto(tiposAto, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(tipo.Id, "VENDA E COMPRA");

        Assert.IsType<ResultadoRenomearTipoAto.Sucesso>(resultado);
    }
}
