namespace Dispatch.Domain;

// Regra confirmada com a operação (documento de requisitos, seção 11, marcava isso como "a
// confirmar"): D+0 vence no fim do dia atual (modelado como início do dia seguinte — mesmo
// instante, forma mais simples de calcular); D+1 são 24 horas corridas a partir da referência,
// D+2 são 48 horas — não "fim do dia seguinte"/"fim de dois dias depois" como a primeira versão
// deste código assumia. "1 hora" fica de fora do ajuste de dia útil abaixo de propósito: é o
// prazo mais urgente do sistema (RF-13, "urgente: prazo 1 hora, D+0 ou prioridade alta"),
// empurrar isso pra depois de um fim de semana contradiz o motivo dele existir.
public sealed record Prazo(TipoPrazo Tipo)
{
    public DateTimeOffset CalcularVencimento(DateTimeOffset momentoDeReferencia) => Tipo switch
    {
        TipoPrazo.UmaHora => momentoDeReferencia.AddHours(1),
        TipoPrazo.D0 => ProximoDiaUtil(FimDoDia(momentoDeReferencia)),
        TipoPrazo.D1 => ProximoDiaUtil(momentoDeReferencia.AddHours(24)),
        TipoPrazo.D2 => ProximoDiaUtil(momentoDeReferencia.AddHours(48)),
        _ => throw new ArgumentOutOfRangeException(nameof(Tipo), Tipo, message: null)
    };

    private static DateTimeOffset FimDoDia(DateTimeOffset referencia) =>
        new DateTimeOffset(referencia.Date, referencia.Offset).AddDays(1);

    // "Considerar dia útil" (pedido explícito da operação): se o vencimento calculado cai num
    // sábado ou domingo, empurra pro próximo dia útil, no mesmo horário — não considera feriado,
    // o sistema ainda não tem calendário de feriados.
    private static DateTimeOffset ProximoDiaUtil(DateTimeOffset data)
    {
        var diasParaEmpurrar = data.DayOfWeek switch
        {
            DayOfWeek.Saturday => 2,
            DayOfWeek.Sunday => 1,
            _ => 0
        };
        return data.AddDays(diasParaEmpurrar);
    }
}
