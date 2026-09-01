using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterCoberturaDeAlcadaTests
{
    private static Protocolo NovoProtocolo(Guid? tipoAtoId) =>
        new(Guid.NewGuid(), "262001", tipoAtoId, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);

    private ObterCoberturaDeAlcada NovoCasoDeUso(
        IReadOnlyCollection<Protocolo> protocolos,
        IReadOnlyCollection<Conferente> conferentes,
        IReadOnlyCollection<RegraAlcada> regras,
        IReadOnlyCollection<TipoAto> tiposAto)
    {
        var obterAlcance = new ObterAlcancePorConferente(
            new FakeConferenteRepository(conferentes), new FakeRegraAlcadaRepository(regras), new FakeTipoAtoRepository(tiposAto),
            new FakeEquipeRepository([]));
        return new ObterCoberturaDeAlcada(
            new FakeProtocoloRepository(protocolos), new FakeConferenteRepository(conferentes), obterAlcance, new FakeTipoAtoRepository(tiposAto));
    }

    [Fact]
    public async Task TipoEmJogoSemNinguemNaEscala_CaiEmSemNinguemHabilitado()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([NovoProtocolo(tipo.Id)], conferentes: [], regras: [], tiposAto: [tipo]);

        var cobertura = await casoDeUso.ExecutarAsync();

        var item = Assert.Single(cobertura.SemNinguemHabilitado);
        Assert.Equal(tipo.Id, item.TipoAtoId);
        Assert.Empty(cobertura.DependeDeUmaPessoa);
    }

    [Fact]
    public async Task TipoComUmSoConferenteNaEscala_CaiEmDependeDeUmaPessoa()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = NovoCasoDeUso([NovoProtocolo(tipo.Id)], conferentes: [conferente], regras: [], tiposAto: [tipo]);

        var cobertura = await casoDeUso.ExecutarAsync();

        var item = Assert.Single(cobertura.DependeDeUmaPessoa);
        Assert.Equal(tipo.Id, item.TipoAtoId);
        Assert.Empty(cobertura.SemNinguemHabilitado);
    }

    [Fact]
    public async Task TipoComDoisConferentesNaEscala_NaoEntraEmNenhumAviso()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferentes = new[]
        {
            new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0),
            new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0),
        };
        var casoDeUso = NovoCasoDeUso([NovoProtocolo(tipo.Id)], conferentes, regras: [], tiposAto: [tipo]);

        var cobertura = await casoDeUso.ExecutarAsync();

        Assert.Empty(cobertura.SemNinguemHabilitado);
        Assert.Empty(cobertura.DependeDeUmaPessoa);
    }

    [Fact]
    public async Task ConferenteComAlcadaMasForaDaEscala_NaoConta()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferenteAusente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: false, cargaAtual: 0);
        var casoDeUso = NovoCasoDeUso([NovoProtocolo(tipo.Id)], conferentes: [conferenteAusente], regras: [], tiposAto: [tipo]);

        var cobertura = await casoDeUso.ExecutarAsync();

        var item = Assert.Single(cobertura.SemNinguemHabilitado);
        Assert.Equal(tipo.Id, item.TipoAtoId);
    }

    [Fact]
    public async Task SemProtocolos_NaoAvisaNada()
    {
        var casoDeUso = NovoCasoDeUso([], conferentes: [], regras: [], tiposAto: []);

        var cobertura = await casoDeUso.ExecutarAsync();

        Assert.Empty(cobertura.SemNinguemHabilitado);
        Assert.Empty(cobertura.DependeDeUmaPessoa);
    }

    [Fact]
    public async Task TipoDesconhecidoForaDoCatalogo_NaoEntraEmNenhumAviso()
    {
        // Protocolo com TipoAtoId nulo (RF-09, tipo desconhecido na importação) — RF-30 é sobre
        // alçada, não sobre catálogo; esse caso já é sinalizado em outro lugar (ResumoImportacao).
        var casoDeUso = NovoCasoDeUso([NovoProtocolo(tipoAtoId: null)], conferentes: [], regras: [], tiposAto: []);

        var cobertura = await casoDeUso.ExecutarAsync();

        Assert.Empty(cobertura.SemNinguemHabilitado);
        Assert.Empty(cobertura.DependeDeUmaPessoa);
    }
}
