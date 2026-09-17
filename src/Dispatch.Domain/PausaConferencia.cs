namespace Dispatch.Domain;

// Uma pausa já encerrada (registrada quando Retomar fecha o intervalo aberto por Pausar) —
// achado numa conversa com o dono, pensando em uso real: nada impedia (nem registrava) alguém
// pausar "de mentira" só pra não contar aquele tempo contra o próprio tempo médio (RF-43/45/46,
// usado numa conta de bonificação externa — ver ObterDashboard.cs). Não bloqueia nem limita
// pausa nenhuma (decisão consciente do dono: time pequeno, visibilidade basta) — só garante que
// quantas vezes e por quanto tempo cada ato ficou pausado fica auditável depois, não descartado.
public sealed class PausaConferencia
{
    public DateTimeOffset PausadoEm { get; }
    public DateTimeOffset RetomadoEm { get; }

    public PausaConferencia(DateTimeOffset pausadoEm, DateTimeOffset retomadoEm)
    {
        PausadoEm = pausadoEm;
        RetomadoEm = retomadoEm;
    }

    public TimeSpan Duracao => RetomadoEm - PausadoEm;
}
