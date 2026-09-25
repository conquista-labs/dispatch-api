namespace Dispatch.Domain.Tests;

// RF-42c — série do período: conferidos por dia útil (semana/mês) ou por semana (trimestre),
// separando os estourados. O período aparece INTEIRO (os dias que não chegaram vêm com
// `Futuro`), e sábado/domingo só entram se alguém conferiu neles.
public class SerieDoPeriodoTests
{
    private static DateTimeOffset Utc(int ano, int mes, int dia, int hora = 0) => new(ano, mes, dia, hora, 0, 0, TimeSpan.Zero);

    private static ConclusaoNaSerie Conclusao(DateTimeOffset concluidoEm, bool estourado = false) => new(concluidoEm, estourado);

    [Fact]
    public void Semana_UmPontoPorDiaUtil_ComFuturosZerados()
    {
        var quartaMeioDia = Utc(2026, 9, 23, 15); // qua 23/09 12h em Brasília

        var serie = SerieDoPeriodo.Montar(PeriodoDashboard.Semana, quartaMeioDia, [
            Conclusao(Utc(2026, 9, 21, 14)),
            Conclusao(Utc(2026, 9, 21, 16), estourado: true),
            Conclusao(Utc(2026, 9, 23, 13)),
        ]);

        Assert.Equal(GranularidadeSerie.Dia, serie.Granularidade);
        Assert.Equal(
            [new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 23), new DateOnly(2026, 9, 24), new DateOnly(2026, 9, 25)],
            serie.Pontos.Select(p => p.Inicio));
        Assert.Equal(new PontoDaSerie(new DateOnly(2026, 9, 21), 2, 1, Futuro: false), serie.Pontos[0]);
        Assert.Equal(new PontoDaSerie(new DateOnly(2026, 9, 22), 0, 0, Futuro: false), serie.Pontos[1]);
        // Hoje não é futuro, mesmo pela metade.
        Assert.Equal(new PontoDaSerie(new DateOnly(2026, 9, 23), 1, 0, Futuro: false), serie.Pontos[2]);
        Assert.All(serie.Pontos.Skip(3), p =>
        {
            Assert.True(p.Futuro);
            Assert.Equal(0, p.Conferidos);
            Assert.Equal(0, p.Estourados);
        });
    }

    // O dia de cada conclusão é o de Brasília: 22/09 01:00 UTC é 21/09 22h local.
    [Fact]
    public void Conclusao_EntraNoDiaDeBrasilia_NaoNoDiaUtc()
    {
        var serie = SerieDoPeriodo.Montar(PeriodoDashboard.Semana, Utc(2026, 9, 23, 15), [Conclusao(Utc(2026, 9, 22, 1))]);

        Assert.Equal(1, serie.Pontos.Single(p => p.Inicio == new DateOnly(2026, 9, 21)).Conferidos);
        Assert.Equal(0, serie.Pontos.Single(p => p.Inicio == new DateOnly(2026, 9, 22)).Conferidos);
    }

    [Fact]
    public void FimDeSemana_SoEntraSeTiverConferencia_NaOrdemDoCalendario()
    {
        var domingoANoite = Utc(2026, 9, 28, 1); // dom 27/09 22h em Brasília

        var serie = SerieDoPeriodo.Montar(PeriodoDashboard.Semana, domingoANoite, [Conclusao(Utc(2026, 9, 26, 14))]);

        Assert.Equal(
            [
                new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 22), new DateOnly(2026, 9, 23), new DateOnly(2026, 9, 24),
                new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 26),
            ],
            serie.Pontos.Select(p => p.Inicio));
        Assert.Equal(1, serie.Pontos[^1].Conferidos);
        Assert.All(serie.Pontos, p => Assert.False(p.Futuro));
    }

    [Fact]
    public void Mes_TodosOsDiasUteisDoMesInteiro()
    {
        var serie = SerieDoPeriodo.Montar(PeriodoDashboard.Mes, Utc(2026, 9, 10, 15), []);

        // Setembro de 2026: 1º é terça, 30 é quarta → 22 dias úteis.
        Assert.Equal(22, serie.Pontos.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), serie.Pontos[0].Inicio);
        Assert.Equal(new DateOnly(2026, 9, 30), serie.Pontos[^1].Inicio);
        Assert.DoesNotContain(serie.Pontos, p => p.Inicio.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday);
        Assert.Equal(8, serie.Pontos.Count(p => !p.Futuro)); // 1–4 e 7–10
    }

    [Fact]
    public void Trimestre_UmPontoPorSemanaComecandoNaSegunda_DoTrimestreInteiro()
    {
        var agora = Utc(2026, 8, 5, 15); // qua 05/08

        var serie = SerieDoPeriodo.Montar(PeriodoDashboard.Trimestre, agora, [
            Conclusao(Utc(2026, 7, 1, 15)),                   // qua 01/07 → semana de seg 29/06
            Conclusao(Utc(2026, 8, 3, 15), estourado: true),  // seg 03/08
            Conclusao(Utc(2026, 8, 5, 14)),                   // qua 05/08 (semana atual)
        ]);

        Assert.Equal(GranularidadeSerie.Semana, serie.Granularidade);
        // T3/2026: 01/07 (qua) a 30/09 (qua) → semanas de 29/06 a 28/09 = 14 pontos.
        Assert.Equal(14, serie.Pontos.Count);
        Assert.All(serie.Pontos, p => Assert.Equal(DayOfWeek.Monday, p.Inicio.DayOfWeek));
        Assert.Equal(new PontoDaSerie(new DateOnly(2026, 6, 29), 1, 0, Futuro: false), serie.Pontos[0]);
        Assert.Equal(new DateOnly(2026, 9, 28), serie.Pontos[^1].Inicio);
        var semanaAtual = serie.Pontos.Single(p => p.Inicio == new DateOnly(2026, 8, 3));
        Assert.Equal(new PontoDaSerie(new DateOnly(2026, 8, 3), 2, 1, Futuro: false), semanaAtual);
        Assert.All(serie.Pontos.Where(p => p.Inicio > new DateOnly(2026, 8, 3)), p => Assert.True(p.Futuro));
    }

    [Fact]
    public void Trimestre_QueComecaNaSegunda_Tem13Semanas()
    {
        // T2/2030: 01/04 é segunda e 30/06 é domingo — as semanas fecham certinho, sem ponta.
        var serie = SerieDoPeriodo.Montar(PeriodoDashboard.Trimestre, Utc(2030, 5, 15, 15), []);

        Assert.Equal(13, serie.Pontos.Count);
        Assert.Equal(new DateOnly(2030, 4, 1), serie.Pontos[0].Inicio);
        Assert.Equal(new DateOnly(2030, 6, 24), serie.Pontos[^1].Inicio);
    }

    // Defensivo: conclusão fora do período (não deveria chegar) é ignorada, não cria ponto.
    [Fact]
    public void ConclusaoForaDoPeriodo_EhIgnorada()
    {
        var serie = SerieDoPeriodo.Montar(PeriodoDashboard.Semana, Utc(2026, 9, 23, 15), [Conclusao(Utc(2026, 9, 19, 15))]);

        Assert.Equal(5, serie.Pontos.Count);
        Assert.All(serie.Pontos, p => Assert.Equal(0, p.Conferidos));
    }
}
