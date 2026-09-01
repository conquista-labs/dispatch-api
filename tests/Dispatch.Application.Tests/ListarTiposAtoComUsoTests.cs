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

        var lista = await casoDeUso.ExecutarAsync();

        var item = Assert.Single(lista);
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

        var lista = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, Assert.Single(lista).ConferentesComAlcada);
    }

    [Fact]
    public async Task ConferenteForaDaEscala_NaoConta()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var conferenteAusente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: false, cargaAtual: 0);
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [], conferentes: [conferenteAusente], regras: []);

        var lista = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, Assert.Single(lista).ConferentesComAlcada);
    }

    [Fact]
    public async Task TipoSemProtocoloNenhum_VolumeZero()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [], conferentes: [], regras: []);

        var lista = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, Assert.Single(lista).Volume);
    }
}
