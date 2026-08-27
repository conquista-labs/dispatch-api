using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class AplicarSugestaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    private static AplicarSugestao NovoCasoDeUso(
        FakeSugestaoRepository sugestoes, FakeTipoAtoRepository? tiposAto = null, FakeEquipeRepository? equipes = null,
        FakeEscreventeRepository? escreventes = null, FakeProtocoloRepository? protocolos = null, FakeRegraAlcadaRepository? regras = null) =>
        new(
            sugestoes,
            tiposAto ?? new FakeTipoAtoRepository([]),
            equipes ?? new FakeEquipeRepository([]),
            escreventes ?? new FakeEscreventeRepository([]),
            protocolos ?? new FakeProtocoloRepository([]),
            regras ?? new FakeRegraAlcadaRepository([]),
            new FakeUnitOfWork(),
            new FakeRelogio(Agora));

    [Fact]
    public async Task SugestaoInexistente_RetornaNaoEncontrada()
    {
        var casoDeUso = NovoCasoDeUso(new FakeSugestaoRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.Equal(ResultadoAplicarSugestao.NaoEncontrada, resultado);
    }

    [Fact]
    public async Task SugestaoJaDecidida_RetornaNaoEstaPendente()
    {
        var sugestao = new Sugestao(
            Guid.NewGuid(), "chave", new PayloadSugestao.TipoDesconhecido("X", Nivel.Pleno), "evidência", 5, Agora);
        sugestao.Aplicar(Agora);
        var casoDeUso = NovoCasoDeUso(new FakeSugestaoRepository([sugestao]));

        var resultado = await casoDeUso.ExecutarAsync(sugestao.Id);

        Assert.Equal(ResultadoAplicarSugestao.NaoEstaPendente, resultado);
    }

    [Fact]
    public async Task TipoDesconhecido_ClassificaOTipoNoCatalogo()
    {
        var sugestao = new Sugestao(
            Guid.NewGuid(), "tipo-desconhecido:ARROLAMENTO",
            new PayloadSugestao.TipoDesconhecido("ARROLAMENTO", Nivel.Pleno), "evidência", 5, Agora);
        var tiposAto = new FakeTipoAtoRepository([]);
        var casoDeUso = NovoCasoDeUso(new FakeSugestaoRepository([sugestao]), tiposAto: tiposAto);

        var resultado = await casoDeUso.ExecutarAsync(sugestao.Id);

        Assert.Equal(ResultadoAplicarSugestao.Sucesso, resultado);
        Assert.Equal(StatusSugestao.Aplicada, sugestao.Status);
        Assert.Equal(1, tiposAto.Quantidade);
        Assert.Contains((await tiposAto.ObterTodosAsync(CancellationToken.None)), t => t.Nome == "ARROLAMENTO");
    }

    [Fact]
    public async Task PrazoIrreal_MudaOPrazoDaEquipeERecalculaAbertos()
    {
        var equipe = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipe.Id);
        var protocoloAberto = new Protocolo(
            Guid.NewGuid(), "1", Guid.NewGuid(), escrevente.Id, Etapa.PreConferencia, Agora.AddDays(-1));
        protocoloAberto.DefinirPrazo(new Prazo(TipoPrazo.D1), Agora.AddDays(-1));

        var sugestao = new Sugestao(
            Guid.NewGuid(), "prazo-irreal:x", new PayloadSugestao.PrazoIrreal(equipe.Id, Etapa.PreConferencia, TipoPrazo.D2),
            "evidência", 8, Agora);

        var casoDeUso = NovoCasoDeUso(
            new FakeSugestaoRepository([sugestao]),
            equipes: new FakeEquipeRepository([equipe]),
            escreventes: new FakeEscreventeRepository([escrevente]),
            protocolos: new FakeProtocoloRepository([protocoloAberto]));

        var resultado = await casoDeUso.ExecutarAsync(sugestao.Id);

        Assert.Equal(ResultadoAplicarSugestao.Sucesso, resultado);
        Assert.Equal(TipoPrazo.D2, equipe.PrazoPreConferencia.Tipo);
        Assert.Equal(TipoPrazo.D1, equipe.PrazoPosConferencia.Tipo);
        Assert.Equal(new Prazo(TipoPrazo.D2).CalcularVencimento(protocoloAberto.AndamentoEm), protocoloAberto.VencimentoEm);
    }

    [Fact]
    public async Task EscreventeOrfao_AlocaNaEquipeSugerida()
    {
        var equipeSugerida = new Equipe(Guid.NewGuid(), "5º andar", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId: null);
        var sugestao = new Sugestao(
            Guid.NewGuid(), "escrevente-orfao:x", new PayloadSugestao.EscreventeOrfao(escrevente.Id, equipeSugerida.Id),
            "evidência", 3, Agora);

        var casoDeUso = NovoCasoDeUso(
            new FakeSugestaoRepository([sugestao]), escreventes: new FakeEscreventeRepository([escrevente]));

        var resultado = await casoDeUso.ExecutarAsync(sugestao.Id);

        Assert.Equal(ResultadoAplicarSugestao.Sucesso, resultado);
        Assert.Equal(equipeSugerida.Id, escrevente.EquipeId);
    }

    [Fact]
    public async Task RiscoQualidade_CriaRegraDeAlcadaAprendida()
    {
        var tipoAtoId = Guid.NewGuid();
        var sugestao = new Sugestao(
            Guid.NewGuid(), "risco-qualidade:x", new PayloadSugestao.RiscoQualidade(tipoAtoId, Nivel.Junior), "evidência", 6, Agora);
        var regras = new FakeRegraAlcadaRepository([]);
        var casoDeUso = NovoCasoDeUso(new FakeSugestaoRepository([sugestao]), regras: regras);

        var resultado = await casoDeUso.ExecutarAsync(sugestao.Id);

        Assert.Equal(ResultadoAplicarSugestao.Sucesso, resultado);
        var regra = Assert.Single(await regras.ObterTodasAsync(CancellationToken.None));
        Assert.Equal(OrigemRegra.Aprendida, regra.Origem);
        Assert.Equal(PermissaoRegra.Nega, regra.Permissao);
        Assert.Equal(Nivel.Junior, ((SujeitoAlcada.PorNivel)regra.Sujeito).Nivel);
        Assert.Equal(tipoAtoId, ((AlvoAlcada.PorTipoAto)regra.Alvo).TipoAtoId);
    }
}
