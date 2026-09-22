namespace Dispatch.Domain;

public enum TipoPrazo
{
    UmaHora,
    D0,
    D1,
    D2,
    // Pedido do dono: "equipe X entra na etapa Y depois das 16h, vence às 10h do dia seguinte" —
    // não é duração desde a entrada como os 4 valores acima, é um horário de corte configurável
    // por Equipe+Etapa (ver Equipe.PrazoPara). Só existe transitoriamente, construído por
    // Equipe.PrazoPara quando o corte já decidiu que a entrada foi depois do horário configurado
    // — nunca é o TipoPrazo base de uma Equipe.
    CorteDeHorario
}
