using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterDetalheProtocoloTests
{
    [Fact]
    public async Task ProtocoloInexistente_RetornaNulo()
    {
        var casoDeUso = new ObterDetalheProtocolo(
            new FakeProtocoloRepository([]), new FakeConferenteRepository([]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.Null(resultado);
    }

    [Fact]
    public async Task TipoConhecidoSemRegraNenhuma_TodosNaEscalaSaoElegiveis()
    {
        var tipoId = Guid.NewGuid();
        var protocolo = new Protocolo(Guid.NewGuid(), "1", tipoId, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new ObterDetalheProtocolo(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.NotNull(resultado);
        var avaliacao = Assert.Single(resultado!.Avaliacoes);
        Assert.True(avaliacao.Elegivel);
    }

    [Fact]
    public async Task TipoDesconhecido_NuncaEhElegivel()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", tipoAtoId: null, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new ObterDetalheProtocolo(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        var avaliacao = Assert.Single(resultado!.Avaliacoes);
        Assert.False(avaliacao.Elegivel);
    }
}
