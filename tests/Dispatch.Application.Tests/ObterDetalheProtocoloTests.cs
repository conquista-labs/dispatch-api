using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterDetalheProtocoloTests
{
    [Fact]
    public async Task ProtocoloInexistente_RetornaNulo()
    {
        var casoDeUso = new ObterDetalheProtocolo(
            new FakeProtocoloRepository([]), new FakeConferenteRepository([]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.Null(resultado);
    }

    [Fact]
    public async Task TipoConhecidoSemRegraNenhuma_TodosNaEscalaSaoElegiveis()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var protocolo = new Protocolo(Guid.NewGuid(), "1", tipo.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new ObterDetalheProtocolo(
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([tipo]));

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
            new FakeProtocoloRepository([protocolo]), new FakeConferenteRepository([conferente]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id);

        var avaliacao = Assert.Single(resultado!.Avaliacoes);
        Assert.False(avaliacao.Elegivel);
    }

    // RF-24k: nº da conferência do protocolo e de cada linha do histórico; a observação da linha
    // reprovada é o "motivo da não aprovação" que o painel mostra (decisão do dono).
    [Fact]
    public async Task Historico_TrazNumeroDaConferenciaEObservacaoDaLinhaReprovada()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var ontem = DateTimeOffset.UtcNow.AddDays(-1);
        var reprovadaAntes = new Protocolo(Guid.NewGuid(), "263546", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, ontem);
        reprovadaAntes.AtribuirA(Guid.NewGuid(), ontem);
        reprovadaAntes.IniciarConferencia(ontem);
        reprovadaAntes.DefinirObservacao("falta certidão atualizada");
        reprovadaAntes.Reprovar(ontem);
        var voltou = new Protocolo(Guid.NewGuid(), "263546", tipo.Id, Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new ObterDetalheProtocolo(
            new FakeProtocoloRepository([reprovadaAntes, voltou]), new FakeConferenteRepository([]), new FakeEscreventeRepository([]),
            new FakeRegraAlcadaRepository([]), new FakeTipoAtoRepository([tipo]));

        var resultado = await casoDeUso.ExecutarAsync(voltou.Id);

        Assert.Equal(2, resultado!.NumeroDaConferencia[voltou.Id]);
        var linha = Assert.Single(resultado.HistoricoConferencias);
        Assert.Equal(1, resultado.NumeroDaConferencia[linha.Id]);
        Assert.Equal("falta certidão atualizada", linha.Observacao);
    }
}
