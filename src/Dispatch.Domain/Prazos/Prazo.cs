namespace Dispatch.Domain;

// Regra confirmada com a operação (documento de requisitos, seção 11, marcava isso como "a
// confirmar"): D+0 vence no fim do dia atual (modelado como início do dia seguinte — mesmo
// instante, forma mais simples de calcular); D+1 são 24 horas corridas a partir da referência,
// D+2 são 48 horas — não "fim do dia seguinte"/"fim de dois dias depois" como a primeira versão
// deste código assumia. "1 hora" fica de fora do ajuste de dia útil abaixo de propósito: é o
// prazo mais urgente do sistema (RF-13, "urgente: prazo 1 hora, D+0 ou prioridade alta"),
// empurrar isso pra depois de um fim de semana contradiz o motivo dele existir.
// HorarioDeVencimento só existe quando Tipo == CorteDeHorario (o horário do dia seguinte, já em
// horário local, que vale como vencimento) — Equipe.PrazoPara já decidiu que a entrada foi
// depois do corte configurado antes de construir este Prazo; CalcularVencimento não reavalia o
// corte, só aplica o horário de vencimento no próximo dia (útil).
public sealed record Prazo(TipoPrazo Tipo, TimeOnly? HorarioDeVencimento = null)
{
    public DateTimeOffset CalcularVencimento(DateTimeOffset momentoDeReferencia) => Tipo switch
    {
        TipoPrazo.UmaHora => momentoDeReferencia.AddHours(1),
        TipoPrazo.D0 => ProximoDiaUtil(FimDoDia(momentoDeReferencia)),
        TipoPrazo.D1 => ProximoDiaUtil(momentoDeReferencia.AddHours(24)),
        TipoPrazo.D2 => ProximoDiaUtil(momentoDeReferencia.AddHours(48)),
        TipoPrazo.CorteDeHorario => ProximoDiaUtil(
            new DateTimeOffset(FusoHorario.ParaHorarioLocal(momentoDeReferencia).Date.AddDays(1), FusoHorario.Brasilia)
                + HorarioDeVencimento!.Value.ToTimeSpan()),
        _ => throw new ArgumentOutOfRangeException(nameof(Tipo), Tipo, message: null)
    };

    // Fim do dia de Brasília, não do dia UTC: a referência chega em UTC, e `.Date` dela dava a
    // meia-noite UTC (21h em Brasília) — e, entre 21h e 24h locais, o fim do dia seguinte.
    private static DateTimeOffset FimDoDia(DateTimeOffset referencia) =>
        FusoHorario.InicioDoDiaLocal(referencia).AddDays(1);

    // "Considerar dia útil" (pedido explícito da operação): se o vencimento calculado cai num
    // sábado ou domingo, empurra pro próximo dia útil, no mesmo horário — não considera feriado,
    // o sistema ainda não tem calendário de feriados. O dia da semana é o de Brasília (sexta 22h
    // local já é sábado em UTC).
    private static DateTimeOffset ProximoDiaUtil(DateTimeOffset data)
    {
        var diasParaEmpurrar = FusoHorario.ParaHorarioLocal(data).DayOfWeek switch
        {
            DayOfWeek.Saturday => 2,
            DayOfWeek.Sunday => 1,
            _ => 0
        };
        return data.AddDays(diasParaEmpurrar);
    }
}
