namespace Dispatch.Domain;

public sealed class Equipe
{
    public Guid Id { get; }
    public string Nome { get; private set; }
    public Prazo PrazoPreConferencia { get; private set; }
    public Prazo PrazoPosConferencia { get; private set; }

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

    // RF-35.
    public void Renomear(string novoNome) => Nome = novoNome;

    // RF-36. RF-38 (recalcular vencimentos abertos) fica pendente — depende de Protocolo
    // saber de qual Escrevente ele veio, o que ainda não existe (ver CLAUDE.md).
    public void DefinirPrazos(Prazo prazoPreConferencia, Prazo prazoPosConferencia)
    {
        PrazoPreConferencia = prazoPreConferencia;
        PrazoPosConferencia = prazoPosConferencia;
    }
}
