using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ListarTiposAtoComUsoTests
{
    private static Protocolo NovoProtocolo(Guid tipoAtoId) =>
        new(Guid.NewGuid(), "262001", tipoAtoId, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);

    private static ListarTiposAtoComUso NovoCasoDeUso(
        IReadOnlyCollection<TipoAto> tiposAto,
        IReadOnlyCollection<Protocolo> protocolos,
        IReadOnlyCollection<Conferente> conferentes,
        IReadOnlyCollection<RegraAlcada> regras)
    {
        var obterAlcance = new ObterAlcancePorConferente(
            new FakeConferenteRepository(conferentes), new FakeRegraAlcadaRepository(regras), new FakeTipoAtoRepository(tiposAto),
            new FakeEquipeRepository([]));
        return new ListarTiposAtoComUso(
            new FakeTipoAtoRepository(tiposAto), new FakeProtocoloRepository(protocolos), new FakeConferenteRepository(conferentes), obterAlcance,
            new FakeConfiguracaoRepository(), new FakeRelogio(DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ContaVolumeEConferentesComAlcada()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = NovoCasoDeUso(
            [tipo], protocolos: [NovoProtocolo(tipo.Id), NovoProtocolo(tipo.Id)], conferentes: [conferente], regras: []);

        var pagina = await casoDeUso.ExecutarAsync();

        Assert.Equal(1, pagina.Total);
        var item = Assert.Single(pagina.Itens);
        Assert.Equal(tipo.Id, item.Id);
        Assert.Equal(2, item.Volume);
        Assert.Equal(1, item.ConferentesComAlcada);
        Assert.Equal(1.00m, item.PesoComplexidade);
        Assert.True(item.Ativo);
    }

    [Fact]
    public async Task ConferenteNegadoNaoConta()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var regraNegando = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipo.Id));
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [], conferentes: [conferente], regras: [regraNegando]);

        var pagina = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, Assert.Single(pagina.Itens).ConferentesComAlcada);
    }

    [Fact]
    public async Task ConferenteForaDaEscala_NaoConta()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferenteAusente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: false, cargaAtual: 0);
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [], conferentes: [conferenteAusente], regras: []);

        var pagina = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, Assert.Single(pagina.Itens).ConferentesComAlcada);
    }

    [Fact]
    public async Task TipoSemProtocoloNenhum_VolumeZero()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [], conferentes: [], regras: []);

        var pagina = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, Assert.Single(pagina.Itens).Volume);
    }

    [Fact]
    public async Task Busca_FiltraPorNomeAntesDePaginar()
    {
        var tipoVenda = new TipoAto(Guid.NewGuid(), "Venda e Compra");
        var tipoDoacao = new TipoAto(Guid.NewGuid(), "Doação");
        var casoDeUso = NovoCasoDeUso([tipoVenda, tipoDoacao], protocolos: [], conferentes: [], regras: []);

        var pagina = await casoDeUso.ExecutarAsync(busca: "venda");

        Assert.Equal(1, pagina.Total);
        Assert.Equal(tipoVenda.Id, Assert.Single(pagina.Itens).Id);
    }

    [Fact]
    public async Task Pagina2_DevolveOsItensSeguintesEOTotalReal()
    {
        var tipos = Enumerable.Range(0, 5).Select(i => new TipoAto(Guid.NewGuid(), $"Tipo {i}")).ToList();
        var casoDeUso = NovoCasoDeUso(tipos, protocolos: [], conferentes: [], regras: []);

        var pagina1 = await casoDeUso.ExecutarAsync(pagina: 1, tamanhoPagina: 2);
        var pagina2 = await casoDeUso.ExecutarAsync(pagina: 2, tamanhoPagina: 2);

        Assert.Equal(5, pagina1.Total);
        Assert.Equal(2, pagina1.Itens.Count);
        Assert.Equal(5, pagina2.Total);
        Assert.Equal(2, pagina2.Itens.Count);
        Assert.NotEqual(pagina1.Itens[0].Id, pagina2.Itens[0].Id);
    }

    // RF-34a/RF-46c: referência efetiva por tipo — informado, mediana (≥ 30 conferências em 12 meses) ou
    // estimativa (TempoMedioPorAtoMinutos 18 das fakes × peso) — com UMA busca de histórico, só da página.
    [Fact]
    public async Task TempoDeReferencia_PorOrigem_EUmaBuscaSoDosTiposDaPagina()
    {
        var comHistorico = new TipoAto(Guid.NewGuid(), "A Escritura");
        var informado = new TipoAto(Guid.NewGuid(), "B Inventário");
        informado.DefinirTempoDeReferencia(38);
        var estimado = new TipoAto(Guid.NewGuid(), "C Procuração", pesoComplexidade: 1.50m);
        var foraDaPagina = new TipoAto(Guid.NewGuid(), "D Ata");
        var conferenteId = Guid.NewGuid();
        var agora = DateTimeOffset.UtcNow;
        var concluidos = Enumerable.Range(0, 30)
            .Select(_ =>
            {
                var p = new Protocolo(Guid.NewGuid(), "1", comHistorico.Id, Guid.NewGuid(), Etapa.PosConferencia, agora.AddDays(-9));
                p.AtribuirA(conferenteId, agora.AddDays(-8));
                p.IniciarConferencia(agora.AddDays(-8));
                p.Aprovar(agora.AddDays(-8).AddMinutes(20));
                return p;
            })
            .ToList();
        var protocolos = new FakeProtocoloRepository(concluidos);
        var tipos = new[] { comHistorico, informado, estimado, foraDaPagina };
        var obterAlcance = new ObterAlcancePorConferente(
            new FakeConferenteRepository([]), new FakeRegraAlcadaRepository([]), new FakeTipoAtoRepository(tipos), new FakeEquipeRepository([]));
        var casoDeUso = new ListarTiposAtoComUso(
            new FakeTipoAtoRepository(tipos), protocolos, new FakeConferenteRepository([]), obterAlcance,
            new FakeConfiguracaoRepository(), new FakeRelogio(agora));

        var pagina = await casoDeUso.ExecutarAsync(pagina: 1, tamanhoPagina: 3);

        Assert.Equal(4, pagina.Total);
        Assert.Equal(new TempoDeReferencia(20, OrigemTempoReferencia.Historico, null, 20, 30), pagina.Itens[0].TempoReferencia);
        Assert.Equal(new TempoDeReferencia(38, OrigemTempoReferencia.Informado, 38, null, 0), pagina.Itens[1].TempoReferencia);
        Assert.Equal(new TempoDeReferencia(27, OrigemTempoReferencia.Estimado, null, null, 0), pagina.Itens[2].TempoReferencia);
        Assert.Equal(1.50m, pagina.Itens[2].PesoComplexidade);
        Assert.Equal(1, protocolos.ChamadasDeDuracoes);
    }
}
