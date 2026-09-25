namespace Dispatch.Domain;

// Dashboard v2 — o período é de CALENDÁRIO no dia de Brasília (decisão do dono, 25/09/2026, ADR-0041):
// "Esta semana" = segunda 00:00 local até agora; "Este mês" = dia 1; "Trimestre" = 1º dia de
// jan/abr/jul/out. Até então era janela móvel de 7/30/90 dias a partir de agora — "Este mês" no dia 3
// mostrava quase todo o mês anterior, e a variação contra o "período anterior" (RF-42b) não teria um
// par natural.
//
// Todo instante sai em UTC (o Npgsql recusa offset ≠ 0 e os instantes viram parâmetro de query);
// o calendário é resolvido em dias locais (DateOnly) via FusoHorario.
public static class CalendarioDoPeriodo
{
    public static IntervaloDoPeriodo Atual(PeriodoDashboard periodo, DateTimeOffset agora) =>
        new(FusoHorario.InicioDoDia(PrimeiroDia(periodo, FusoHorario.DiaLocal(agora))), agora.ToUniversalTime());

    // RF-42b — "variação contra o período anterior equivalente": o MESMO TRECHO do período anterior.
    // Início anterior = início atual menos um período (no calendário local); fim anterior = início
    // anterior + o quanto já passou do período atual, nunca além do início atual — no dia 31/03 o
    // trecho passaria de fevereiro inteiro, então compara com fevereiro todo.
    public static IntervaloDoPeriodo MesmoTrechoAnterior(PeriodoDashboard periodo, DateTimeOffset agora)
    {
        var atual = Atual(periodo, agora);
        var primeiroDiaAtual = PrimeiroDia(periodo, FusoHorario.DiaLocal(agora));
        var inicioAnterior = FusoHorario.InicioDoDia(Voltar(periodo, primeiroDiaAtual));
        var fimAnterior = inicioAnterior + (atual.Fim - atual.Inicio);
        return new IntervaloDoPeriodo(inicioAnterior, fimAnterior < atual.Inicio ? fimAnterior : atual.Inicio);
    }

    public static DateOnly PrimeiroDia(PeriodoDashboard periodo, DateOnly hoje) => periodo switch
    {
        PeriodoDashboard.Semana => SegundaDaSemana(hoje),
        PeriodoDashboard.Mes => new DateOnly(hoje.Year, hoje.Month, 1),
        PeriodoDashboard.Trimestre => new DateOnly(hoje.Year, ((hoje.Month - 1) / 3 * 3) + 1, 1),
        _ => throw new ArgumentOutOfRangeException(nameof(periodo), periodo, message: null)
    };

    // Último dia (inclusive) do período INTEIRO — a série (RF-42c) mostra os dias que ainda não chegaram.
    public static DateOnly UltimoDia(PeriodoDashboard periodo, DateOnly hoje)
    {
        var primeiro = PrimeiroDia(periodo, hoje);
        return periodo switch
        {
            PeriodoDashboard.Semana => primeiro.AddDays(6),
            PeriodoDashboard.Mes => primeiro.AddMonths(1).AddDays(-1),
            PeriodoDashboard.Trimestre => primeiro.AddMonths(3).AddDays(-1),
            _ => throw new ArgumentOutOfRangeException(nameof(periodo), periodo, message: null)
        };
    }

    // Semana de segunda a domingo (DayOfWeek começa no domingo = 0).
    public static DateOnly SegundaDaSemana(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    private static DateOnly Voltar(PeriodoDashboard periodo, DateOnly primeiroDia) => periodo switch
    {
        PeriodoDashboard.Semana => primeiroDia.AddDays(-7),
        PeriodoDashboard.Mes => primeiroDia.AddMonths(-1),
        PeriodoDashboard.Trimestre => primeiroDia.AddMonths(-3),
        _ => throw new ArgumentOutOfRangeException(nameof(periodo), periodo, message: null)
    };
}

// Instantes UTC; Fim exclusivo (o repositório busca ConcluidoEm >= Inicio && < Fim).
public sealed record IntervaloDoPeriodo(DateTimeOffset Inicio, DateTimeOffset Fim);
