namespace Dispatch.Domain;

// Todo instante do sistema é guardado/comparado em UTC (Prazo.CalcularVencimento, IRelogio.Agora,
// o próprio Postgres via DateTimeOffsetParaUtcConverter) — mas "corte de horário" (Equipe.PrazoPara)
// é uma regra operacional do cartório, pensada em horário de parede local. Sem converter antes de
// comparar, a regra dispararia ~3h adiantada/atrasada. Fixo em America/Sao_Paulo (sem horário de
// verão desde 2019) — sem TimeZoneInfo/calendário, mesma filosofia de "sem feriado" já usada em
// Prazo.ProximoDiaUtil.
internal static class FusoHorario
{
    public static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    public static DateTimeOffset ParaHorarioLocal(DateTimeOffset instante) => instante.ToOffset(Brasilia);
}
