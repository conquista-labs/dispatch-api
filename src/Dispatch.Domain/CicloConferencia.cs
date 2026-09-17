namespace Dispatch.Domain;

// Um ciclo de conferência já encerrado — criado toda vez que ReabrirConferencia fecha o ciclo
// que estava rodando, guardando quem foi o dono daquele ciclo específico. Existe pra duas coisas
// não conflitarem: a Duracao final do protocolo precisa somar todos os ciclos (achado em
// produção, ver Protocolo.Duracao), mas o Dashboard (RF-43/45/46) usa esse mesmo tempo pra medir
// carga/produtividade de cada conferente — um acumulador cego (só a soma, sem saber de quem)
// faria um ato reaberto e reatribuído (RF-24c, só acontece quando o dono original saiu da
// escala, RF-27) jogar o tempo do ciclo de uma pessoa na conta de outra.
public sealed class CicloConferencia
{
    public Guid ConferenteId { get; }
    public DateTimeOffset IniciadoEm { get; }
    public DateTimeOffset ConcluidoEm { get; }

    public CicloConferencia(Guid conferenteId, DateTimeOffset iniciadoEm, DateTimeOffset concluidoEm)
    {
        ConferenteId = conferenteId;
        IniciadoEm = iniciadoEm;
        ConcluidoEm = concluidoEm;
    }

    public TimeSpan Duracao => ConcluidoEm - IniciadoEm;
}
