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

    // Achado em produção: conferente Pleno com a regra própria "Nega etapa PosConferencia" (só faz
    // pré) aparecia com 0 tipos — "Tipos permitidos" era avaliado só em Pós. Regras de nível
    // reproduzindo o caso real: Permite TodosOsAtos + Permite TipoAto + Nega EquipeEEtapa (Pré,
    // Quinto Andar).
    [Fact]
    public async Task PessoaQueNegaPosConferencia_AlcancaOsTiposPelaPreConferencia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var inventario = new TipoAto(Guid.NewGuid(), "Inventário");
        var escritura = new TipoAto(Guid.NewGuid(), "Escritura");
        var quintoAndar = new Equipe(Guid.NewGuid(), "Quinto Andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var rio = new Equipe(Guid.NewGuid(), "Equipe RIO", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var regras = new[]
        {
            new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Pleno), PermissaoRegra.Permite, new AlvoAlcada.PorTodosOsAtos()),
            new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Pleno), PermissaoRegra.Permite, new AlvoAlcada.PorTipoAto(inventario.Id)),
            new RegraAlcada(
                Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Pleno), PermissaoRegra.Nega,
                new AlvoAlcada.PorEquipeEEtapa(quintoAndar.Id, Etapa.PreConferencia)),
            new RegraAlcada(
                Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferente.Id), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PosConferencia)),
        };
        var casoDeUso = new ObterAlcancePorConferente(
            new FakeConferenteRepository([conferente]), new FakeRegraAlcadaRepository(regras),
            new FakeTipoAtoRepository([inventario, escritura]), new FakeEquipeRepository([quintoAndar, rio]));

        var alcance = await casoDeUso.ExecutarAsync();

        var doConferente = Assert.Single(alcance);
        Assert.Equal([inventario.Id, escritura.Id], doConferente.TiposPermitidosIds);
        Assert.Equal([Etapa.PreConferencia], doConferente.EtapasPermitidas);
        // Equipes avaliadas em Pré (a etapa que ela faz): Quinto Andar não faz pré, as outras sim.
        Assert.Equal([rio.Id, null], doConferente.EquipesPermitidasIds);
    }

    [Fact]
    public async Task SemRestricaoDeEtapa_EquipesContinuamAvaliadasNaPrimeiraEtapaLiberada()
    {
        // Regressão: quem não tem restrição de etapa continua alcançando tipo, as duas etapas e
        // todas as equipes (inclusive "sem equipe").
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var equipe = new Equipe(Guid.NewGuid(), "Equipe RIO", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var casoDeUso = new ObterAlcancePorConferente(
            new FakeConferenteRepository([conferente]), new FakeRegraAlcadaRepository([]), new FakeTipoAtoRepository([tipo]),
            new FakeEquipeRepository([equipe]));

        var alcance = await casoDeUso.ExecutarAsync();

        var doConferente = Assert.Single(alcance);
        Assert.Equal([tipo.Id], doConferente.TiposPermitidosIds);
        Assert.Equal([Etapa.PreConferencia, Etapa.PosConferencia], doConferente.EtapasPermitidas);
        Assert.Equal([equipe.Id, null], doConferente.EquipesPermitidasIds);
    }

    [Fact]
    public async Task TipoNegadoNasDuasEtapas_ContinuaForaDosTiposPermitidos()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var liberado = new TipoAto(Guid.NewGuid(), "Procuração");
        var negado = new TipoAto(Guid.NewGuid(), "Inventário");
        var regra = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(negado.Id));
        var casoDeUso = new ObterAlcancePorConferente(
            new FakeConferenteRepository([conferente]), new FakeRegraAlcadaRepository([regra]),
            new FakeTipoAtoRepository([liberado, negado]), new FakeEquipeRepository([]));

        var alcance = await casoDeUso.ExecutarAsync();

        Assert.Equal([liberado.Id], Assert.Single(alcance).TiposPermitidosIds);
    }
}
