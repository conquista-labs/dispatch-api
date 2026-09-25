namespace Dispatch.Domain.Tests;

// RF-42b (metas "configuráveis em Configuração do sistema") e RF-46 ("pesos configuráveis"): o que
// a Configuração aceita. Metas são frações entre 0,50 e 1,00 (abaixo de 50% a barra de meta deixa de
// dizer alguma coisa); pesos são inteiros ≥ 0 que somam exatamente 100 — a escala do score e das
// faixas 85/70 continua sendo 0–100.
public class MetasEPesosTests
{
    private static Configuracao NovaConfiguracao() => new(
        Guid.NewGuid(), TimeSpan.FromHours(4), TimeSpan.FromMinutes(60), 1, TimeSpan.FromMinutes(15),
        30, 18, 5, 8, 0.6, 3, 6, 0.5);

    [Fact]
    public void ConfiguracaoNova_NasceComOsPadroesDoPrototipo()
    {
        var configuracao = NovaConfiguracao();

        Assert.Equal(new MetasDoDashboard(0.95, 0.90), configuracao.Metas);
        Assert.Equal(new PesosDoScore(40, 30, 20, 10), configuracao.Pesos);
        Assert.Equal(MetasDoDashboard.Padrao, configuracao.Metas);
        Assert.Equal(PesosDoScore.Padrao, configuracao.Pesos);
    }

    [Theory]
    [InlineData(0.50, 0.50)]
    [InlineData(1.00, 1.00)]
    [InlineData(0.95, 0.90)]
    public void MetasNasBordasOuDentro_SaoValidas(double noPrazo, double aprovadoNaPrimeira) =>
        Assert.Null(new MetasDoDashboard(noPrazo, aprovadoNaPrimeira).Validar());

    [Theory]
    [InlineData(0.49, 0.90, "metaNoPrazo")]
    [InlineData(1.01, 0.90, "metaNoPrazo")]
    [InlineData(0.95, 0.4999, "metaAprovadoNaPrimeira")]
    [InlineData(0.95, 95, "metaAprovadoNaPrimeira")] // percentual inteiro por engano — fração é 0–1
    [InlineData(double.NaN, 0.90, "metaNoPrazo")]
    public void MetaForaDaFaixa_TemMotivoQueNomeiaOCampo(double noPrazo, double aprovadoNaPrimeira, string campo)
    {
        var motivo = new MetasDoDashboard(noPrazo, aprovadoNaPrimeira).Validar();

        Assert.NotNull(motivo);
        Assert.StartsWith(campo, motivo);
    }

    [Theory]
    [InlineData(40, 30, 20, 10)]
    [InlineData(100, 0, 0, 0)]
    [InlineData(25, 25, 25, 25)]
    public void PesosQueSomam100_SaoValidos(int volume, int prazo, int qualidade, int complexidade) =>
        Assert.Null(new PesosDoScore(volume, prazo, qualidade, complexidade).Validar());

    [Theory]
    [InlineData(40, 30, 20, 20, "110")]
    [InlineData(40, 30, 20, 0, "90")]
    [InlineData(0, 0, 0, 0, "0")]
    public void PesosQueNaoSomam100_MotivoDizQuantoSomam(int volume, int prazo, int qualidade, int complexidade, string soma)
    {
        var motivo = new PesosDoScore(volume, prazo, qualidade, complexidade).Validar();

        Assert.NotNull(motivo);
        Assert.Contains("somar 100", motivo);
        Assert.Contains(soma, motivo);
    }

    [Fact]
    public void PesoNegativo_ERejeitadoMesmoSomando100()
    {
        // 50 + 40 + 20 − 10 = 100: a soma sozinha deixaria passar uma parcela que TIRA pontos.
        var motivo = new PesosDoScore(50, 40, 20, -10).Validar();

        Assert.NotNull(motivo);
        Assert.StartsWith("pesoComplexidade", motivo);
    }

    [Fact]
    public void DefinirMetasEPesos_Validos_TrocaOsSeisValores()
    {
        var configuracao = NovaConfiguracao();

        configuracao.DefinirMetasEPesos(new MetasDoDashboard(0.80, 0.75), new PesosDoScore(10, 20, 30, 40));

        Assert.Equal(0.80, configuracao.MetaNoPrazo);
        Assert.Equal(0.75, configuracao.MetaAprovadoNaPrimeira);
        Assert.Equal(10, configuracao.PesoVolume);
        Assert.Equal(20, configuracao.PesoPrazo);
        Assert.Equal(30, configuracao.PesoQualidade);
        Assert.Equal(40, configuracao.PesoComplexidade);
    }

    [Fact]
    public void DefinirMetasEPesos_Invalidos_RecusaSemMudarNada()
    {
        // Guarda do invariante: a Application valida antes e devolve 400 com motivo; se um caminho
        // novo esquecer, a entidade não aceita ficar com pesos que não somam 100.
        var configuracao = NovaConfiguracao();

        Assert.Throws<ArgumentException>(() =>
            configuracao.DefinirMetasEPesos(MetasDoDashboard.Padrao, new PesosDoScore(40, 30, 20, 20)));
        Assert.Throws<ArgumentException>(() =>
            configuracao.DefinirMetasEPesos(new MetasDoDashboard(0.30, 0.90), PesosDoScore.Padrao));

        Assert.Equal(MetasDoDashboard.Padrao, configuracao.Metas);
        Assert.Equal(PesosDoScore.Padrao, configuracao.Pesos);
    }
}
