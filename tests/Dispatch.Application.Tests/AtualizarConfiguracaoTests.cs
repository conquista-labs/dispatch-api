using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class AtualizarConfiguracaoTests
{
    private static AtualizarConfiguracao NovoCasoDeUso(out FakeConfiguracaoRepository configuracao)
    {
        configuracao = new FakeConfiguracaoRepository();
        return new AtualizarConfiguracao(configuracao, new FakeUnitOfWork());
    }

    [Fact]
    public async Task ValoresValidos_AtualizaEDevolveSucesso()
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);

        var resultado = await casoDeUso.ExecutarAsync(
            TimeSpan.FromHours(2), TimeSpan.FromMinutes(30), 2, TimeSpan.FromMinutes(20), 15, 20, 4, 7, 0.5, 2, 5, 0.4);

        Assert.IsType<ResultadoAtualizarConfiguracao.Sucesso>(resultado);
        var atual = await configuracao.ObterAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.FromHours(2), atual.FaixaAtencao);
        Assert.Equal(2, atual.LimiteDeAtosSimultaneos);
        Assert.Equal(0.4, atual.LimiarRiscoQualidadeReprovacao);
    }

    [Theory]
    [InlineData(0, 60, 1, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // faixaAtencao zero
    [InlineData(240, 60, 0, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // limiteDeAtosSimultaneos < 1
    [InlineData(240, 60, 1, 15, -1, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // diasDeMemoriaDescarte negativo
    [InlineData(240, 60, 1, 15, 30, 18.0, 5, 8, 1.5, 3, 6, 0.5)] // limiarPrazoIrrealEstouro fora de 0-1
    [InlineData(60, 60, 1, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // faixaUrgente igual a faixaAtencao
    [InlineData(60, 240, 1, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // faixaUrgente maior que faixaAtencao
    public async Task ValorInvalido_RejeitaSemAlterarNada(
        int faixaAtencaoMinutos, int faixaUrgenteMinutos, int limiteDeAtosSimultaneos, int janelaDeCorrecaoMinutos,
        int diasDeMemoriaDescarte, double tempoMedioPorAtoMinutos, int limiarTipoDesconhecido, int limiarPrazoIrrealCasos,
        double limiarPrazoIrrealEstouro, int limiarEscreventeOrfao, int limiarRiscoQualidadeCasos, double limiarRiscoQualidadeReprovacao)
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);
        var original = await configuracao.ObterAsync(CancellationToken.None);
        var faixaAtencaoOriginal = original.FaixaAtencao;

        var resultado = await casoDeUso.ExecutarAsync(
            TimeSpan.FromMinutes(faixaAtencaoMinutos), TimeSpan.FromMinutes(faixaUrgenteMinutos), limiteDeAtosSimultaneos,
            TimeSpan.FromMinutes(janelaDeCorrecaoMinutos), diasDeMemoriaDescarte, tempoMedioPorAtoMinutos, limiarTipoDesconhecido,
            limiarPrazoIrrealCasos, limiarPrazoIrrealEstouro, limiarEscreventeOrfao, limiarRiscoQualidadeCasos,
            limiarRiscoQualidadeReprovacao);

        Assert.IsType<ResultadoAtualizarConfiguracao.ValorInvalido>(resultado);
        Assert.Equal(faixaAtencaoOriginal, original.FaixaAtencao);
    }

    // Os 12 valores operacionais válidos, iguais aos do fake — os testes abaixo só variam metas/pesos.
    private static Task<ResultadoAtualizarConfiguracao> AtualizarAsync(
        AtualizarConfiguracao casoDeUso, double? metaNoPrazo = null, double? metaAprovadoNaPrimeira = null,
        int? pesoVolume = null, int? pesoPrazo = null, int? pesoQualidade = null, int? pesoComplexidade = null,
        int faixaAtencaoMinutos = 240) =>
        casoDeUso.ExecutarAsync(
            TimeSpan.FromMinutes(faixaAtencaoMinutos), TimeSpan.FromMinutes(60), 1, TimeSpan.FromMinutes(15), 30, 18, 5, 8, 0.6, 3, 6, 0.5,
            metaNoPrazo, metaAprovadoNaPrimeira, pesoVolume, pesoPrazo, pesoQualidade, pesoComplexidade);

    [Fact]
    public async Task SemMetasNemPesos_MantemOsAtuais()
    {
        // O front anterior manda o PUT só com os 12 — não pode zerar pesos nem metas.
        var casoDeUso = NovoCasoDeUso(out var configuracao);
        var atual = await configuracao.ObterAsync(CancellationToken.None);
        atual.DefinirMetasEPesos(new MetasDoDashboard(0.85, 0.80), new PesosDoScore(25, 25, 25, 25));

        var resultado = await AtualizarAsync(casoDeUso, faixaAtencaoMinutos: 180);

        Assert.IsType<ResultadoAtualizarConfiguracao.Sucesso>(resultado);
        Assert.Equal(TimeSpan.FromMinutes(180), atual.FaixaAtencao);
        Assert.Equal(new MetasDoDashboard(0.85, 0.80), atual.Metas);
        Assert.Equal(new PesosDoScore(25, 25, 25, 25), atual.Pesos);
    }

    [Fact]
    public async Task SoParteDasMetasEPesos_TrocaOQueVeioEMantemORestante()
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);

        // 40/30/20/10 → volume 30, complexidade 20 (prazo e qualidade ficam): soma 100.
        var resultado = await AtualizarAsync(casoDeUso, metaNoPrazo: 0.9, pesoVolume: 30, pesoComplexidade: 20);

        Assert.IsType<ResultadoAtualizarConfiguracao.Sucesso>(resultado);
        var atual = await configuracao.ObterAsync(CancellationToken.None);
        Assert.Equal(new MetasDoDashboard(0.9, 0.90), atual.Metas);
        Assert.Equal(new PesosDoScore(30, 30, 20, 20), atual.Pesos);
    }

    [Fact]
    public async Task OsSeisInformados_GravaTodos()
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);

        var resultado = await AtualizarAsync(casoDeUso, 0.5, 1.0, 0, 50, 50, 0);

        Assert.IsType<ResultadoAtualizarConfiguracao.Sucesso>(resultado);
        var atual = await configuracao.ObterAsync(CancellationToken.None);
        Assert.Equal(new MetasDoDashboard(0.5, 1.0), atual.Metas);
        Assert.Equal(new PesosDoScore(0, 50, 50, 0), atual.Pesos);
    }

    [Theory]
    [InlineData(null, null, 50, null, null, null, "somar 100")] // 50+30+20+10 = 110
    [InlineData(null, null, 50, 30, 30, -10, "pesoComplexidade")] // soma 100 com negativo
    [InlineData(0.4, null, null, null, null, null, "metaNoPrazo")]
    [InlineData(null, 1.2, null, null, null, null, "metaAprovadoNaPrimeira")]
    public async Task MetasOuPesosInvalidos_DevolveMotivoENaoMudaNada(
        double? metaNoPrazo, double? metaAprovadoNaPrimeira, int? pesoVolume, int? pesoPrazo, int? pesoQualidade,
        int? pesoComplexidade, string trechoDoMotivo)
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);
        var atual = await configuracao.ObterAsync(CancellationToken.None);

        var resultado = await AtualizarAsync(
            casoDeUso, metaNoPrazo, metaAprovadoNaPrimeira, pesoVolume, pesoPrazo, pesoQualidade, pesoComplexidade,
            faixaAtencaoMinutos: 180);

        var invalido = Assert.IsType<ResultadoAtualizarConfiguracao.MetasOuPesosInvalidos>(resultado);
        Assert.Contains(trechoDoMotivo, invalido.Motivo);
        // Nem os 12 operacionais (que vieram válidos) nem metas/pesos mudam.
        Assert.Equal(TimeSpan.FromHours(4), atual.FaixaAtencao);
        Assert.Equal(MetasDoDashboard.Padrao, atual.Metas);
        Assert.Equal(PesosDoScore.Padrao, atual.Pesos);
    }

    // ADR-0046: a regra do pool entra no PUT como opcional — o front anterior não manda e não pode
    // resetar o que o administrador escolheu.
    private static Task<ResultadoAtualizarConfiguracao> AtualizarRegraDoPoolAsync(
        AtualizarConfiguracao casoDeUso, int? limiteDeAtosNaMao, bool? poolEmOrdemObrigatoria, int faixaAtencaoMinutos = 240) =>
        casoDeUso.ExecutarAsync(
            TimeSpan.FromMinutes(faixaAtencaoMinutos), TimeSpan.FromMinutes(60), 1, TimeSpan.FromMinutes(15), 30, 18, 5, 8, 0.6, 3, 6, 0.5,
            limiteDeAtosNaMao: limiteDeAtosNaMao, poolEmOrdemObrigatoria: poolEmOrdemObrigatoria);

    [Fact]
    public async Task RegraDoPool_Informada_Atualiza()
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);

        var resultado = await AtualizarRegraDoPoolAsync(casoDeUso, limiteDeAtosNaMao: 3, poolEmOrdemObrigatoria: false);

        Assert.IsType<ResultadoAtualizarConfiguracao.Sucesso>(resultado);
        var atual = await configuracao.ObterAsync(CancellationToken.None);
        Assert.Equal(3, atual.LimiteDeAtosNaMao);
        Assert.False(atual.PoolEmOrdemObrigatoria);
    }

    [Fact]
    public async Task RegraDoPool_Ausente_MantemAAtual()
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);
        var atual = await configuracao.ObterAsync(CancellationToken.None);
        atual.DefinirRegraDoPool(limiteDeAtosNaMao: 2, poolEmOrdemObrigatoria: false);

        var resultado = await AtualizarRegraDoPoolAsync(casoDeUso, limiteDeAtosNaMao: null, poolEmOrdemObrigatoria: null);

        Assert.IsType<ResultadoAtualizarConfiguracao.Sucesso>(resultado);
        Assert.Equal(2, atual.LimiteDeAtosNaMao);
        Assert.False(atual.PoolEmOrdemObrigatoria);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task LimiteNaMaoAbaixoDeUm_RejeitaComMotivoSemMudarNada(int limite)
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);
        var atual = await configuracao.ObterAsync(CancellationToken.None);

        var resultado = await AtualizarRegraDoPoolAsync(casoDeUso, limite, poolEmOrdemObrigatoria: false, faixaAtencaoMinutos: 180);

        var invalido = Assert.IsType<ResultadoAtualizarConfiguracao.ValorInvalido>(resultado);
        Assert.Contains("limiteDeAtosNaMao", invalido.Motivo);
        Assert.Equal(5, atual.LimiteDeAtosNaMao);
        Assert.True(atual.PoolEmOrdemObrigatoria);
        Assert.Equal(TimeSpan.FromHours(4), atual.FaixaAtencao);
    }
}
