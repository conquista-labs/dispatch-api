using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterAlcancePorConferenteTests
{
    [Fact]
    public async Task SemRegraNenhuma_AlcancaTudoPorPadraoAberto()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = new ObterAlcancePorConferente(
            new FakeConferenteRepository([conferente]), new FakeRegraAlcadaRepository([]), new FakeTipoAtoRepository([tipo]),
            new FakeEquipeRepository([]));

        var alcance = await casoDeUso.ExecutarAsync();

        var doConferente = Assert.Single(alcance);
        Assert.Equal(2, doConferente.EtapasPermitidas.Count);
        Assert.Contains(tipo.Id, doConferente.TiposPermitidosIds);
    }

    [Fact]
    public async Task RegraDeNivelNegandoEtapa_ExcluiEssaEtapaDoAlcance()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var regra = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));
        var casoDeUso = new ObterAlcancePorConferente(
            new FakeConferenteRepository([conferente]), new FakeRegraAlcadaRepository([regra]), new FakeTipoAtoRepository([tipo]),
            new FakeEquipeRepository([]));

        var alcance = await casoDeUso.ExecutarAsync();

        var doConferente = Assert.Single(alcance);
        Assert.DoesNotContain(Etapa.PreConferencia, doConferente.EtapasPermitidas);
        Assert.Contains(Etapa.PosConferencia, doConferente.EtapasPermitidas);
    }
}
