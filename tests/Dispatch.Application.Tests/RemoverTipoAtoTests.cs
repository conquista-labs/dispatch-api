using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class RemoverTipoAtoTests
{
    private static Protocolo NovoProtocolo(Guid tipoAtoId) =>
        new(Guid.NewGuid(), "262001", tipoAtoId, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);

    private static RemoverTipoAto NovoCasoDeUso(
        IReadOnlyCollection<TipoAto> tiposAto,
        IReadOnlyCollection<Protocolo> protocolos,
        IReadOnlyCollection<RegraAlcada> regras,
        out FakeTipoAtoRepository repositorioTipos) =>
        new(
            repositorioTipos = new FakeTipoAtoRepository(tiposAto),
            new FakeProtocoloRepository(protocolos),
            new FakeRegraAlcadaRepository(regras),
            new FakeUnitOfWork());

    [Fact]
    public async Task SemUsoNenhum_Remove()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [], regras: [], out var tiposAto);

        var resultado = await casoDeUso.ExecutarAsync(tipo.Id);

        Assert.IsType<ResultadoRemoverTipoAto.Sucesso>(resultado);
        Assert.Equal(0, tiposAto.Quantidade);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var casoDeUso = NovoCasoDeUso([], protocolos: [], regras: [], out _);

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.IsType<ResultadoRemoverTipoAto.NaoEncontrado>(resultado);
    }

    [Fact]
    public async Task ComProtocoloReferenciando_EmUso()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [NovoProtocolo(tipo.Id)], regras: [], out var tiposAto);

        var resultado = await casoDeUso.ExecutarAsync(tipo.Id);

        Assert.IsType<ResultadoRemoverTipoAto.EmUso>(resultado);
        Assert.Equal(1, tiposAto.Quantidade);
    }

    [Fact]
    public async Task ComRegraDeAlcadaApontandoPraEle_EmUso()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var regra = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipo.Id));
        var casoDeUso = NovoCasoDeUso([tipo], protocolos: [], regras: [regra], out var tiposAto);

        var resultado = await casoDeUso.ExecutarAsync(tipo.Id);

        Assert.IsType<ResultadoRemoverTipoAto.EmUso>(resultado);
        Assert.Equal(1, tiposAto.Quantidade);
    }
}
