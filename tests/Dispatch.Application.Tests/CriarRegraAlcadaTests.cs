using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CriarRegraAlcadaTests
{
    [Fact]
    public async Task RegraPorNivelETipoConhecido_Cria()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var regras = new FakeRegraAlcadaRepository([]);
        var casoDeUso = new CriarRegraAlcada(
            regras, new FakeConferenteRepository([]), new FakeTipoAtoRepository([tipo]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipo.Id));

        Assert.IsType<ResultadoCriarRegraAlcada.Sucesso>(resultado);
        Assert.Equal(1, regras.Quantidade);
    }

    [Fact]
    public async Task SujeitoPorPessoaComConferenteInexistente_Rejeita()
    {
        var casoDeUso = new CriarRegraAlcada(
            new FakeRegraAlcadaRepository([]), new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorPessoa(Guid.NewGuid()), PermissaoRegra.Permite, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

        Assert.IsType<ResultadoCriarRegraAlcada.ConferenteNaoEncontrado>(resultado);
    }

    [Fact]
    public async Task AlvoPorTipoAtoInexistente_Rejeita()
    {
        var casoDeUso = new CriarRegraAlcada(
            new FakeRegraAlcadaRepository([]), new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Pleno), PermissaoRegra.Permite, new AlvoAlcada.PorTipoAto(Guid.NewGuid()));

        Assert.IsType<ResultadoCriarRegraAlcada.TipoAtoNaoEncontrado>(resultado);
    }
}
