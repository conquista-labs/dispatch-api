namespace Dispatch.Domain;

public sealed class Protocolo
{
    public Guid Id { get; }
    public string Numero { get; }
    // Nulo = tipo de ato desconhecido (RF-09) — sinalizado, não inventado na hora. O motor de
    // distribuição já trata isso como "tipo desconhecido" (exceção) sem precisar de FK válida.
    public Guid? TipoAtoId { get; }
    public Etapa Etapa { get; }
    public Prioridade Prioridade { get; }
    // Instante do "andamento" que originou este registro (vem do relatório importado, não de
    // quando a importação rodou) — é o momentoDeReferencia usado pra calcular o vencimento, e
    // também a base da "linha de corte" que evita reimportar o que já foi processado.
    public DateTimeOffset AndamentoEm { get; }
    // Nulo quando o protocolo nasce fora de um lote (o endpoint avulso /protocolos/distribuir,
    // por exemplo) — RF-13 filtra "visão deste lote" por aqui.
    public Guid? LoteImportacaoId { get; }
    public Prazo? Prazo { get; private set; }
    public DateTimeOffset? VencimentoEm { get; private set; }
    public StatusProtocolo Status { get; private set; } = StatusProtocolo.Pool;
    public Guid? DonoId { get; private set; }
    public string? MotivoExcecao { get; private set; }
    public string? Observacao { get; private set; }

    public Protocolo(
        Guid id, string numero, Guid? tipoAtoId, Etapa etapa, DateTimeOffset andamentoEm,
        Prioridade prioridade = Prioridade.Normal, Guid? loteImportacaoId = null)
    {
        Id = id;
        Numero = numero;
        TipoAtoId = tipoAtoId;
        Etapa = etapa;
        AndamentoEm = andamentoEm;
        Prioridade = prioridade;
        LoteImportacaoId = loteImportacaoId;
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

    public void AtribuirA(Guid conferenteId)
    {
        Status = StatusProtocolo.Atribuido;
        DonoId = conferenteId;
        MotivoExcecao = null;
    }

    // Também usado pelo RF-27: quando o dono fica ausente ou é removido, o protocolo dele
    // volta pro pool — é a mesma transição, não importa o motivo de ter perdido o dono.
    public void EnviarParaPool()
    {
        Status = StatusProtocolo.Pool;
        DonoId = null;
        MotivoExcecao = null;
    }

    public void MarcarExcecao(string motivo)
    {
        Status = StatusProtocolo.Excecao;
        DonoId = null;
        MotivoExcecao = motivo;
    }

    // RF-17: descartar uma exceção que não vale a pena resolver. Mantém MotivoExcecao —
    // é o registro de por que ela existiu, não faz sentido apagar isso ao descartar.
    public void Descartar()
    {
        Status = StatusProtocolo.Descartado;
        DonoId = null;
    }

    // RF-15/RF-23: editável em qualquer estado, por isso não tem guarda de status nenhuma aqui.
    public void DefinirObservacao(string? observacao) => Observacao = observacao;
}
