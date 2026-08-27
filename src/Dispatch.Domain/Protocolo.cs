namespace Dispatch.Domain;

public sealed class Protocolo
{
    public Guid Id { get; }
    public string Numero { get; }
    // Nulo = tipo de ato desconhecido (RF-09) — sinalizado, não inventado na hora. O motor de
    // distribuição já trata isso como "tipo desconhecido" (exceção) sem precisar de FK válida.
    public Guid? TipoAtoId { get; }
    // Preenchido só quando TipoAtoId é nulo — o texto bruto que veio do relatório (RF-09/
    // seção 7 "Tipo desconhecido"). Sem isso não dá pra agrupar "quantas vezes 'X' apareceu
    // fora do catálogo" pra gerar a sugestão de aprendizado.
    public string? TipoAtoNomeOriginal { get; }
    // Quem produziu o ato (glossário, seção 2) — RF-14 (o card mostra escrevente/equipe) e
    // RF-38 (recalcular vencimento quando o prazo da equipe do escrevente muda) dependem disso.
    public Guid EscreventeId { get; }
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
    public DateTimeOffset? IniciadoEm { get; private set; }
    public DateTimeOffset? ConcluidoEm { get; private set; }

    // RF-24: "duração" do ato — só existe depois de concluído.
    public TimeSpan? Duracao => IniciadoEm is { } inicio && ConcluidoEm is { } fim ? fim - inicio : null;

    public Protocolo(
        Guid id, string numero, Guid? tipoAtoId, Guid escreventeId, Etapa etapa, DateTimeOffset andamentoEm,
        Prioridade prioridade = Prioridade.Normal, Guid? loteImportacaoId = null, string? tipoAtoNomeOriginal = null)
    {
        Id = id;
        Numero = numero;
        TipoAtoId = tipoAtoId;
        TipoAtoNomeOriginal = tipoAtoId is null ? tipoAtoNomeOriginal : null;
        EscreventeId = escreventeId;
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

    // RF-21: arranca o cronômetro. Quem decide se pode iniciar (é do conferente certo, tá
    // Atribuido, respeita o limite de simultâneos) é o caso de uso — aqui só a transição.
    public void IniciarConferencia(DateTimeOffset agora)
    {
        Status = StatusProtocolo.Conferindo;
        IniciadoEm = agora;
    }

    // RF-22: aprovar ou não aprovar encerra o ato e grava a duração (via ConcluidoEm/Duracao).
    public void Aprovar(DateTimeOffset agora)
    {
        Status = StatusProtocolo.Aprovado;
        ConcluidoEm = agora;
    }

    public void Reprovar(DateTimeOffset agora)
    {
        Status = StatusProtocolo.Reprovado;
        ConcluidoEm = agora;
    }
}
