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
            new FakeTipoAtoRepository(tiposAto), new FakeProtocoloRepository(protocolos), new FakeConferenteRepository(conferentes), obterAlcance);
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
        Assert.Equal(1, item.PesoComplexidade);
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
}
