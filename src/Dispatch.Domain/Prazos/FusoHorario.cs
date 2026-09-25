namespace Dispatch.Domain;

// Todo instante do sistema é guardado/comparado em UTC (Prazo.CalcularVencimento, IRelogio.Agora,
// o próprio Postgres via DateTimeOffsetParaUtcConverter) — mas "corte de horário" (Equipe.PrazoPara),
// "fim do dia" (D+0), "dia útil" e "hoje" (concluídos hoje) são regras operacionais do cartório,
// pensadas em horário de parede local. Sem converter antes de comparar, a regra dispararia ~3h
// adiantada/atrasada (e, entre 21h e 24h de Brasília, no dia errado). Fixo em America/Sao_Paulo
// (sem horário de verão desde 2019) — sem TimeZoneInfo/calendário, mesma filosofia de "sem
// feriado" já usada em Prazo.ProximoDiaUtil. Público porque a Application também precisa de
// "hoje" (ObterConcluidosHoje, ObterVisaoDistribuicao): é o único lugar que sabe o fuso.
public static class FusoHorario
{
    public static readonly TimeSpan Brasilia = TimeSpan.FromHours(-3);

    public static DateTimeOffset ParaHorarioLocal(DateTimeOffset instante) => instante.ToOffset(Brasilia);

    // Meia-noite (em Brasília) do dia local do instante, devolvida em UTC — pronta pra comparar
    // com instantes gravados e pra virar parâmetro de query (o Npgsql recusa offset ≠ 0).
    public static DateTimeOffset InicioDoDiaLocal(DateTimeOffset instante) =>
        new DateTimeOffset(ParaHorarioLocal(instante).Date, Brasilia).ToUniversalTime();
}
