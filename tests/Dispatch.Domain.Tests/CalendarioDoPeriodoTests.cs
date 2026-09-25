namespace Dispatch.Domain.Tests;

// Dashboard v2 (decisão do dono de 25/09/2026): o período é de CALENDÁRIO no dia de Brasília, não
// janela móvel. "Esta semana" = segunda 00:00 local até agora; "Este mês" = dia 1; "Trimestre" = 1º
// dia de jan/abr/jul/out. A variação (RF-42b) compara com o MESMO TRECHO do período anterior.
public class CalendarioDoPeriodoTests
{
    // Brasília é UTC−3 fixo: meia-noite local = 03:00 UTC.
    private static DateTimeOffset Utc(int ano, int mes, int dia, int hora = 0, int minuto = 0) =>
        new(ano, mes, dia, hora, minuto, 0, TimeSpan.Zero);

    [Fact]
    public void Semana_ComecaNaSegundaMeiaNoiteDeBrasilia()
    {
        var quintaMeioDia = Utc(2026, 9, 24, 15); // qui 24/09 12h em Brasília

        var intervalo = CalendarioDoPeriodo.Atual(PeriodoDashboard.Semana, quintaMeioDia);

        Assert.Equal(Utc(2026, 9, 21, 3), intervalo.Inicio); // seg 21/09 00:00 local
        Assert.Equal(quintaMeioDia, intervalo.Fim);
    }

    // Borda do fuso: segunda 01:00 UTC ainda é domingo 22h em Brasília — a semana é a que está
    // acabando (começou na segunda anterior), não a que o relógio UTC já abriu.
    [Fact]
    public void Semana_DomingoANoiteEmBrasilia_QueJaEhSegundaEmUtc_AindaEhASemanaQueAcaba()
    {
        var segundaUmaDaManhaUtc = Utc(2026, 9, 28, 1); // dom 27/09 22h em Brasília

        var intervalo = CalendarioDoPeriodo.Atual(PeriodoDashboard.Semana, segundaUmaDaManhaUtc);

        Assert.Equal(Utc(2026, 9, 21, 3), intervalo.Inicio);
    }

    [Fact]
    public void Semana_NaPropriaSegundaDeManha_ComecaNaMeiaNoiteDaquelaSegunda()
    {
        var segundaNoveDaManha = Utc(2026, 9, 28, 12); // seg 28/09 09h em Brasília

        var intervalo = CalendarioDoPeriodo.Atual(PeriodoDashboard.Semana, segundaNoveDaManha);

        Assert.Equal(Utc(2026, 9, 28, 3), intervalo.Inicio);
    }

    [Fact]
    public void Mes_ComecaNoDiaPrimeiroMeiaNoiteDeBrasilia()
    {
        var intervalo = CalendarioDoPeriodo.Atual(PeriodoDashboard.Mes, Utc(2026, 9, 25, 18));

        Assert.Equal(Utc(2026, 9, 1, 3), intervalo.Inicio);
    }

    // Virada de mês: 01/10 02:00 UTC ainda é 30/09 23h em Brasília — o mês é setembro.
    [Fact]
    public void Mes_ViradaDoMesEmUtcAntesDeBrasilia_AindaEhOMesQueAcaba()
    {
        var intervalo = CalendarioDoPeriodo.Atual(PeriodoDashboard.Mes, Utc(2026, 10, 1, 2));

        Assert.Equal(Utc(2026, 9, 1, 3), intervalo.Inicio);
    }

    [Theory]
    [InlineData(2, 14, 1)]
    [InlineData(4, 1, 4)]
    [InlineData(6, 30, 4)]
    [InlineData(9, 25, 7)]
    [InlineData(12, 31, 10)]
    public void Trimestre_ComecaEmJaneiroAbrilJulhoOuOutubro(int mes, int dia, int mesDeInicio)
    {
        var intervalo = CalendarioDoPeriodo.Atual(PeriodoDashboard.Trimestre, Utc(2026, mes, dia, 15));

        Assert.Equal(Utc(2026, mesDeInicio, 1, 3), intervalo.Inicio);
    }

    [Fact]
    public void Intervalo_SaiEmUtcMesmoComEntradaEmOutroOffset()
    {
        var emBrasilia = new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.FromHours(-3));

        var intervalo = CalendarioDoPeriodo.Atual(PeriodoDashboard.Mes, emBrasilia);

        Assert.Equal(TimeSpan.Zero, intervalo.Inicio.Offset);
        Assert.Equal(TimeSpan.Zero, intervalo.Fim.Offset);
        Assert.Equal(Utc(2026, 9, 25, 15), intervalo.Fim);
    }

    // "Mesmo trecho": dia 25/09 12h local → 01/08 00:00 até 25/08 12h local.
    [Fact]
    public void MesmoTrechoAnterior_Mes_VaiDoDiaPrimeiroAteOMesmoPontoDoMesPassado()
    {
        var agora = Utc(2026, 9, 25, 15);

        var anterior = CalendarioDoPeriodo.MesmoTrechoAnterior(PeriodoDashboard.Mes, agora);

        Assert.Equal(Utc(2026, 8, 1, 3), anterior.Inicio);
        Assert.Equal(Utc(2026, 8, 25, 15), anterior.Fim);
    }

    // Mês anterior mais curto: 31/03 12h local já passou de fevereiro inteiro (30d12h > 28d) — o fim
    // anterior é limitado ao início do período atual, então compara com fevereiro todo.
    [Fact]
    public void MesmoTrechoAnterior_MesAnteriorMaisCurto_ParaNoInicioDoPeriodoAtual()
    {
        var agora = Utc(2026, 3, 31, 15);

        var anterior = CalendarioDoPeriodo.MesmoTrechoAnterior(PeriodoDashboard.Mes, agora);

        Assert.Equal(Utc(2026, 2, 1, 3), anterior.Inicio);
        Assert.Equal(Utc(2026, 3, 1, 3), anterior.Fim);
    }

    [Fact]
    public void MesmoTrechoAnterior_Semana_EhASemanaPassadaAteOMesmoDiaEHora()
    {
        var quintaMeioDia = Utc(2026, 9, 24, 15);

        var anterior = CalendarioDoPeriodo.MesmoTrechoAnterior(PeriodoDashboard.Semana, quintaMeioDia);

        Assert.Equal(Utc(2026, 9, 14, 3), anterior.Inicio);
        Assert.Equal(Utc(2026, 9, 17, 15), anterior.Fim);
    }

    // Trimestre anterior com a virada de ano: 15/01 → 01/10 do ano anterior até 15/10.
    [Fact]
    public void MesmoTrechoAnterior_Trimestre_AtravessaAViradaDoAno()
    {
        var agora = Utc(2027, 1, 15, 15);

        var anterior = CalendarioDoPeriodo.MesmoTrechoAnterior(PeriodoDashboard.Trimestre, agora);

        Assert.Equal(Utc(2026, 10, 1, 3), anterior.Inicio);
        Assert.Equal(Utc(2026, 10, 15, 15), anterior.Fim);
    }

    // Logo no primeiro instante do período, o trecho anterior tem duração zero (início == fim).
    [Fact]
    public void MesmoTrechoAnterior_NoPrimeiroInstanteDoPeriodo_TemDuracaoZero()
    {
        var meiaNoiteDoDiaPrimeiro = Utc(2026, 9, 1, 3);

        var anterior = CalendarioDoPeriodo.MesmoTrechoAnterior(PeriodoDashboard.Mes, meiaNoiteDoDiaPrimeiro);

        Assert.Equal(anterior.Inicio, anterior.Fim);
        Assert.Equal(Utc(2026, 8, 1, 3), anterior.Inicio);
    }

    [Theory]
    [InlineData(PeriodoDashboard.Semana, 2026, 9, 27)] // domingo da semana de 21/09
    [InlineData(PeriodoDashboard.Mes, 2026, 9, 30)]
    [InlineData(PeriodoDashboard.Trimestre, 2026, 9, 30)]
    public void UltimoDia_EhOFimDoPeriodoInteiro(PeriodoDashboard periodo, int ano, int mes, int dia)
    {
        var hoje = new DateOnly(2026, 9, 24);

        Assert.Equal(new DateOnly(ano, mes, dia), CalendarioDoPeriodo.UltimoDia(periodo, hoje));
    }

    [Fact]
    public void UltimoDia_FevereiroDeAnoBissexto()
    {
        Assert.Equal(new DateOnly(2028, 2, 29), CalendarioDoPeriodo.UltimoDia(PeriodoDashboard.Mes, new DateOnly(2028, 2, 10)));
    }
}
