namespace Dispatch.Domain.Tests;

// RF-46c + RF-34a com as decisões do dono (25/09/2026): precedência informado → mediana → estimativa;
// mediana só com ≥ 30 conferências válidas nos últimos 12 meses, descartando as de duração > 4× a
// estimativa; estimativa = round(TempoMedioPorAtoMinutos × peso). Peso de complexidade decimal
// 0,50–2,50 em passos de 0,05 (RF-34f); tempo informado 2–240 min.
public class TempoDeReferenciaTests
{
    private const double TempoMedioPorAto = 15;

    private static IEnumerable<TimeSpan> Minutos(int quantidade, double minutos) =>
        Enumerable.Repeat(TimeSpan.FromMinutes(minutos), quantidade);

    // --- Estimativa ---

    [Theory]
    [InlineData(15, 1.00, 15)]
    [InlineData(15, 1.50, 23)] // 22,5 → 23: arredonda "pra longe do zero", não o bancário (que daria 22)
    [InlineData(15, 1.25, 19)] // 18,75
    [InlineData(15, 0.50, 8)]  // 7,5 → 8
    [InlineData(18, 2.50, 45)]
    public void Estimativa_EhTempoMedioVezesPeso_Arredondada(double tempoMedio, double peso, int esperado) =>
        Assert.Equal(esperado, TempoDeReferencia.Estimativa(tempoMedio, (decimal)peso));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(0.4)]
    public void Estimativa_NuncaFicaAbaixoDeUmMinuto(double tempoMedio) =>
        // Configuração sem validação do tempo médio: referência 0 faria o ritmo dividir por zero.
        Assert.Equal(1, TempoDeReferencia.Estimativa(tempoMedio, 0.50m));

    // --- Precedência ---

    [Fact]
    public void TipoSemHistorico_UsaEstimativa()
    {
        var referencia = TempoDeReferencia.Calcular(informadoMinutos: null, 1.50m, TempoMedioPorAto, []);

        Assert.Equal(23, referencia.Minutos);
        Assert.Equal(OrigemTempoReferencia.Estimado, referencia.Origem);
        Assert.Null(referencia.InformadoMinutos);
        Assert.Null(referencia.MedianaMinutos);
        Assert.Equal(0, referencia.ConferenciasNoHistorico);
    }

    [Fact]
    public void Com29Conferencias_AindaEhEstimativa_MasContaAsConferencias()
    {
        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, Minutos(29, 20));

        Assert.Equal(OrigemTempoReferencia.Estimado, referencia.Origem);
        Assert.Equal(15, referencia.Minutos);
        Assert.Null(referencia.MedianaMinutos);
        Assert.Equal(29, referencia.ConferenciasNoHistorico);
    }

    [Fact]
    public void Com30Conferencias_PassaAUsarAMediana()
    {
        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, Minutos(30, 20));

        Assert.Equal(OrigemTempoReferencia.Historico, referencia.Origem);
        Assert.Equal(20, referencia.Minutos);
        Assert.Equal(20, referencia.MedianaMinutos);
        Assert.Equal(30, referencia.ConferenciasNoHistorico);
    }

    [Fact]
    public void Informado_VenceAMediana_QueContinuaCalculadaPraUsarHistorico()
    {
        var referencia = TempoDeReferencia.Calcular(informadoMinutos: 40, 1.00m, TempoMedioPorAto, Minutos(30, 20));

        Assert.Equal(OrigemTempoReferencia.Informado, referencia.Origem);
        Assert.Equal(40, referencia.Minutos);
        Assert.Equal(40, referencia.InformadoMinutos);
        // "usar histórico" (RF-34a) precisa saber que a mediana existe e quanto ela vale.
        Assert.Equal(20, referencia.MedianaMinutos);
    }

    [Fact]
    public void Informado_VenceAEstimativa()
    {
        var referencia = TempoDeReferencia.Calcular(informadoMinutos: 7, 2.00m, TempoMedioPorAto, []);

        Assert.Equal(OrigemTempoReferencia.Informado, referencia.Origem);
        Assert.Equal(7, referencia.Minutos);
        Assert.Null(referencia.MedianaMinutos);
    }

    // --- Mediana ---

    [Fact]
    public void Mediana_ComQuantidadePar_EhAMediaDosDoisDoMeio_Arredondada()
    {
        // 15 de 10 min e 15 de 13 min: os do meio são 10 e 13 → 11,5 → 12.
        var duracoes = Minutos(15, 10).Concat(Minutos(15, 13));

        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, duracoes);

        Assert.Equal(12, referencia.MedianaMinutos);
    }

    [Fact]
    public void Mediana_ComQuantidadeImpar_EhODoMeio_ENaoAMedia()
    {
        // 16 de 10 min + 15 de 50 min (≤ 4×15 = 60, ficam): a média seria ~29, a mediana é 10.
        var duracoes = Minutos(16, 10).Concat(Minutos(15, 50));

        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, duracoes);

        Assert.Equal(31, referencia.ConferenciasNoHistorico);
        Assert.Equal(10, referencia.MedianaMinutos);
    }

    [Fact]
    public void Mediana_ArredondaSegundos()
    {
        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, Minutos(30, 12.5));

        Assert.Equal(13, referencia.MedianaMinutos);
    }

    [Fact]
    public void Mediana_NuncaFicaAbaixoDeUmMinuto()
    {
        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, Minutos(30, 0.2));

        Assert.Equal(OrigemTempoReferencia.Historico, referencia.Origem);
        Assert.Equal(1, referencia.MedianaMinutos);
    }

    // --- Descarte (> 4× a estimativa) ---

    [Fact]
    public void Descarte_TiraAsAcimaDe4VezesAEstimativa_EAsExatamente4VezesFicam()
    {
        // Estimativa 15 → limite 60 min. 30 de 60 min (ficam) + 5 de 60 min e 1 s (saem).
        var duracoes = Minutos(30, 60).Concat(Enumerable.Repeat(TimeSpan.FromMinutes(60) + TimeSpan.FromSeconds(1), 5));

        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, duracoes);

        Assert.Equal(30, referencia.ConferenciasNoHistorico);
        Assert.Equal(60, referencia.MedianaMinutos);
    }

    [Fact]
    public void Descarte_ContaAntesDoMinimo_30ComDescartadasNaoBastam()
    {
        // 29 válidas + 3 "esquecidas abertas" (8 h): só 29 contam → continua estimativa.
        var duracoes = Minutos(29, 20).Concat(Minutos(3, 480));

        var referencia = TempoDeReferencia.Calcular(null, 1.00m, TempoMedioPorAto, duracoes);

        Assert.Equal(29, referencia.ConferenciasNoHistorico);
        Assert.Equal(OrigemTempoReferencia.Estimado, referencia.Origem);
    }

    [Fact]
    public void Descarte_UsaAEstimativaDoPeso_NaoAReferenciaInformada()
    {
        // Peso 2,00 → estimativa 30 → limite 120. Informado 10 não muda o limite (decisão do dono:
        // "descartando as de duração > 4× a estimativa").
        var duracoes = Minutos(30, 100);

        var referencia = TempoDeReferencia.Calcular(informadoMinutos: 10, 2.00m, TempoMedioPorAto, duracoes);

        Assert.Equal(30, referencia.ConferenciasNoHistorico);
        Assert.Equal(100, referencia.MedianaMinutos);
    }

    // --- Validações ---

    [Theory]
    [InlineData(null)]
    [InlineData(2)]
    [InlineData(240)]
    [InlineData(38)]
    public void TempoInformado_NasBordasOuNulo_EhValido(int? minutos) =>
        Assert.Null(TempoDeReferencia.ValidarInformado(minutos));

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(241)]
    [InlineData(-10)]
    public void TempoInformado_ForaDaFaixa_TemMotivo(int minutos) =>
        Assert.StartsWith("minutos", TempoDeReferencia.ValidarInformado(minutos));

    [Theory]
    [InlineData(0.50)]
    [InlineData(2.50)]
    [InlineData(1.00)]
    [InlineData(1.35)]
    [InlineData(1.05)]
    public void Peso_NaFaixaEMultiploDe005_EhValido(double peso) =>
        Assert.Null(PesoDeComplexidade.Validar((decimal)peso));

    [Theory]
    [InlineData(0.45)]
    [InlineData(2.55)]
    [InlineData(0)]
    [InlineData(3)]
    public void Peso_ForaDaFaixa_TemMotivo(double peso) =>
        Assert.Contains("entre 0,50 e 2,50", PesoDeComplexidade.Validar((decimal)peso));

    [Theory]
    [InlineData(1.33)]
    [InlineData(1.01)]
    [InlineData(1.025)]
    public void Peso_ForaDoPasso_TemMotivo(double peso) =>
        Assert.Contains("0,05", PesoDeComplexidade.Validar((decimal)peso));

    [Fact]
    public void TipoAto_RecusaPesoInvalido_EAceitaDecimal()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        Assert.Equal(1.00m, tipo.PesoComplexidade);

        tipo.DefinirPesoDeComplexidade(1.75m);
        Assert.Equal(1.75m, tipo.PesoComplexidade);

        Assert.Throws<ArgumentException>(() => tipo.DefinirPesoDeComplexidade(3m));
        Assert.Throws<ArgumentException>(() => new TipoAto(Guid.NewGuid(), "X", pesoComplexidade: 1.33m));
        Assert.Equal(1.75m, tipo.PesoComplexidade);
    }

    [Fact]
    public void TipoAto_TempoInformado_ValidaEAceitaNulo()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        Assert.Null(tipo.TempoReferenciaMinutos);

        tipo.DefinirTempoDeReferencia(38);
        Assert.Equal(38, tipo.TempoReferenciaMinutos);

        Assert.Throws<ArgumentException>(() => tipo.DefinirTempoDeReferencia(1));
        Assert.Equal(38, tipo.TempoReferenciaMinutos);

        tipo.DefinirTempoDeReferencia(null);
        Assert.Null(tipo.TempoReferenciaMinutos);
    }
}
