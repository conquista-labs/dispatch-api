namespace Dispatch.Domain;

public sealed class Protocolo
{
    public Guid Id { get; }
    public string Numero { get; }
    public Guid TipoAtoId { get; }
    public Etapa Etapa { get; }
    public Prioridade Prioridade { get; }
    public Prazo? Prazo { get; private set; }
    public DateTimeOffset? VencimentoEm { get; private set; }

    public Protocolo(Guid id, string numero, Guid tipoAtoId, Etapa etapa, Prioridade prioridade = Prioridade.Normal)
    {
        Id = id;
        Numero = numero;
        TipoAtoId = tipoAtoId;
        Etapa = etapa;
        Prioridade = prioridade;
    }

    // Prazo e vencimento não entram no construtor porque, no fluxo real, só existem depois
    // de resolver o escrevente contra a equipe dele (ResolvedorDePrazo) — e podem ser
    // recalculados depois (RF-38: mudar o prazo de uma equipe recalcula vencimentos abertos).
    public void DefinirPrazo(Prazo prazo, DateTimeOffset momentoDeReferencia)
    {
        Prazo = prazo;
        VencimentoEm = prazo.CalcularVencimento(momentoDeReferencia);
    }

    // Seção 4: urgente é prioridade alta OU prazo curto (1 hora ou D+0).
    public bool Urgente =>
        Prioridade == Prioridade.Alta ||
        Prazo is { Tipo: TipoPrazo.UmaHora or TipoPrazo.D0 };
}
