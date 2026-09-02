using Dispatch.Domain;

namespace Dispatch.Application.Tests;

// Achado numa auditoria de qualidade do front: o simulador "Testar" (dispatch-web,
// AbaAlcadaTestar.tsx) inferia o destino só pela contagem de elegíveis, sem rodar o motor de
// verdade — a regra real decide primeiro por urgência (Prioridade.Alta), não por contagem.
// Estes 3 testes cobrem exatamente os casos que provavam a diferença.
public class SimularAlcadaTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static SimularAlcada NovoCasoDeUso(
        IReadOnlyCollection<Conferente> conferentes, IReadOnlyCollection<RegraAlcada> regras,
        IReadOnlyCollection<TipoAto> tiposAto, IReadOnlyCollection<Equipe> equipes) =>
        new(new FakeConferenteRepository(conferentes), new FakeRegraAlcadaRepository(regras), new FakeTipoAtoRepository(tiposAto),
            new FakeEquipeRepository(equipes), new FakeRelogio(Agora));

    [Fact]
    public async Task NaoUrgenteComUmElegivel_VaiParaOPool_NaoAtribuiDireto()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var equipe = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = NovoCasoDeUso([conferente], [], [tipo], [equipe]);

        var resultado = await casoDeUso.ExecutarAsync(Etapa.PosConferencia, tipo.Id, equipe.Id, Prioridade.Normal);

        Assert.Equal("EnviadoParaPool", resultado!.Destino);
        Assert.Null(resultado.ConferenteId);
    }

    [Fact]
    public async Task UrgenteComMultiplosElegiveis_AtribuiAoDeMenorCarga()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var equipe = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var maisCarregado = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 5);
        var menosCarregado = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 1);
        var casoDeUso = NovoCasoDeUso([maisCarregado, menosCarregado], [], [tipo], [equipe]);

        var resultado = await casoDeUso.ExecutarAsync(Etapa.PosConferencia, tipo.Id, equipe.Id, Prioridade.Alta);

        Assert.Equal("Atribuido", resultado!.Destino);
        Assert.Equal(menosCarregado.Id, resultado.ConferenteId);
    }

    [Fact]
    public async Task SemNinguemNaEscala_VaiParaExcecao()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([], [], [tipo], []);

        var resultado = await casoDeUso.ExecutarAsync(Etapa.PosConferencia, tipo.Id, null, Prioridade.Normal);

        Assert.Equal("Excecao", resultado!.Destino);
        Assert.Equal("ninguém com alçada", resultado.Motivo);
    }

    [Fact]
    public async Task TipoAtoInexistente_RetornaNulo()
    {
        var casoDeUso = NovoCasoDeUso([], [], [], []);

        var resultado = await casoDeUso.ExecutarAsync(Etapa.PosConferencia, Guid.NewGuid(), null, Prioridade.Normal);

        Assert.Null(resultado);
    }
}
