using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DistribuirProtocoloTests
{
    private static readonly TipoAto Inventario = new(Guid.NewGuid(), "Inventário");
    private static readonly DateTimeOffset Agora = new(2026, 8, 26, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EscreventeComEquipeDePrazoUrgente_ResolvePrazoEAtribuiAoUnicoElegivel()
    {
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);
        var conferente = new Conferente(Guid.NewGuid(), Nivel.Pleno, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Etapa.PreConferencia);

        var caso = new DistribuirProtocolo(
            new FakeConferenteRepository([conferente]),
            new FakeEquipeRepository([equipe]),
            new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([Inventario]),
            new FakeRelogio(Agora));

        var resultado = await caso.ExecutarAsync(protocolo, escrevente);

        Assert.Equal(TipoPrazo.D0, protocolo.Prazo?.Tipo);
        Assert.Equal(new DateTimeOffset(Agora.Date, Agora.Offset).AddDays(1), protocolo.VencimentoEm);
        var atribuido = Assert.IsType<ResultadoDistribuicao.Atribuido>(resultado);
        Assert.Equal(conferente.Id, atribuido.Conferente.Id);
    }

    [Fact]
    public async Task EscreventeSemEquipe_CaiNoPadraoD1EVaiParaOPool()
    {
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId: null);
        var conferente = new Conferente(Guid.NewGuid(), Nivel.Pleno, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "124", Inventario.Id, Etapa.PosConferencia);

        var caso = new DistribuirProtocolo(
            new FakeConferenteRepository([conferente]),
            new FakeEquipeRepository([]),
            new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([Inventario]),
            new FakeRelogio(Agora));

        var resultado = await caso.ExecutarAsync(protocolo, escrevente);

        Assert.Equal(TipoPrazo.D1, protocolo.Prazo?.Tipo);
        Assert.IsType<ResultadoDistribuicao.EnviadoParaPool>(resultado);
    }

    [Fact]
    public async Task TipoDeAtoDesconhecido_RetornaExcecao()
    {
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId: null);
        var protocolo = new Protocolo(Guid.NewGuid(), "125", Guid.NewGuid(), Etapa.PreConferencia);

        var caso = new DistribuirProtocolo(
            new FakeConferenteRepository([]),
            new FakeEquipeRepository([]),
            new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([Inventario]),
            new FakeRelogio(Agora));

        var resultado = await caso.ExecutarAsync(protocolo, escrevente);

        Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
    }
}
