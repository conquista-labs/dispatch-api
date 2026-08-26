namespace Dispatch.Domain;

public sealed class Equipe
{
    public Guid Id { get; }
    public string Nome { get; }
    public Prazo PrazoPreConferencia { get; }
    public Prazo PrazoPosConferencia { get; }

    public Equipe(Guid id, string nome, Prazo prazoPreConferencia, Prazo prazoPosConferencia)
    {
        Id = id;
        Nome = nome;
        PrazoPreConferencia = prazoPreConferencia;
        PrazoPosConferencia = prazoPosConferencia;
    }

    public Prazo PrazoPara(Etapa etapa) => etapa switch
    {
        Etapa.PreConferencia => PrazoPreConferencia,
        Etapa.PosConferencia => PrazoPosConferencia,
        _ => throw new ArgumentOutOfRangeException(nameof(etapa), etapa, message: null)
    };
}
