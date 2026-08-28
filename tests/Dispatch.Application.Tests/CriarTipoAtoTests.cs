namespace Dispatch.Application.Tests;

public class CriarTipoAtoTests
{
    [Fact]
    public async Task NomeNovo_NormalizaECadastra()
    {
        var tiposAto = new FakeTipoAtoRepository([]);
        var casoDeUso = new CriarTipoAto(tiposAto, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("VENDA E COMPRA");

        var sucesso = Assert.IsType<ResultadoCriarTipoAto.Sucesso>(resultado);
        Assert.Equal(1, tiposAto.Quantidade);
        var criado = (await tiposAto.ObterTodosAsync(CancellationToken.None)).Single();
        Assert.Equal(sucesso.TipoAtoId, criado.Id);
        Assert.Equal("Venda e Compra", criado.Nome);
    }

    [Fact]
    public async Task NomeJaExistente_MesmoComCaixaDiferente_NaoDuplica()
    {
        var existente = new Dispatch.Domain.TipoAto(Guid.NewGuid(), "Venda e Compra");
        var tiposAto = new FakeTipoAtoRepository([existente]);
        var casoDeUso = new CriarTipoAto(tiposAto, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("VENDA E COMPRA");

        Assert.IsType<ResultadoCriarTipoAto.JaExiste>(resultado);
        Assert.Equal(1, tiposAto.Quantidade);
    }
}
