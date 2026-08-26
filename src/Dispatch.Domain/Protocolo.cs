namespace Dispatch.Domain;

public sealed class Protocolo
{
    public Guid Id { get; }
    public string Numero { get; }
    public Guid TipoAtoId { get; }
    public Etapa Etapa { get; }
    public Prioridade Prioridade { get; }

    public Protocolo(Guid id, string numero, Guid tipoAtoId, Etapa etapa, Prioridade prioridade = Prioridade.Normal)
    {
        Id = id;
        Numero = numero;
        TipoAtoId = tipoAtoId;
        Etapa = etapa;
        Prioridade = prioridade;
    }

    // Urgência hoje só reflete prioridade alta. O gatilho por prazo (1h, D+0 — seção 5 do
    // documento de requisitos) entra quando Prazo/Vencimento forem modelados no domínio.
    public bool Urgente => Prioridade == Prioridade.Alta;
}
