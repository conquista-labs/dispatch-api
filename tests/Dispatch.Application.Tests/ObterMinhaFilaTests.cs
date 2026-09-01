using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterMinhaFilaTests
{
    [Fact]
    public async Task PoolDisponivel_SoTraProtocolosDentroDaAlcada()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var permitido = new Protocolo(Guid.NewGuid(), "1", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        var negado = new Protocolo(Guid.NewGuid(), "2", tipo.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var regra = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([permitido, negado]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([regra]),
            new FakeTipoAtoRepository([tipo]));

        var fila = await casoDeUso.ExecutarAsync(conferente);

        var resultado = Assert.Single(fila.PoolDisponivel);
        Assert.Equal(permitido.Id, resultado.Id);
    }

    [Fact]
    public async Task AtribuidosEEmConferencia_SoDoProprioConferente()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var outroConferenteId = Guid.NewGuid();

        var atribuido = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        atribuido.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);

        var emConferencia = new Protocolo(Guid.NewGuid(), "2", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        emConferencia.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        emConferencia.IniciarConferencia(DateTimeOffset.UtcNow);

        var deOutroConferente = new Protocolo(Guid.NewGuid(), "3", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        deOutroConferente.AtribuirA(outroConferenteId, DateTimeOffset.UtcNow);

        var casoDeUso = new ObterMinhaFila(
            new FakeProtocoloRepository([atribuido, emConferencia, deOutroConferente]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([]));

        var fila = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal([atribuido.Id], fila.Atribuidos.Select(p => p.Id));
        Assert.Equal([emConferencia.Id], fila.EmConferencia.Select(p => p.Id));
    }
}
