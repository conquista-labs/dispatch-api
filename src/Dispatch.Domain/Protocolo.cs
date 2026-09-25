namespace Dispatch.Domain;

public sealed class Protocolo
{
    public Guid Id { get; }
    public string Numero { get; }
    // Nulo = tipo de ato desconhecido (RF-09) — sinalizado, não inventado na hora. O motor de
    // distribuição já trata isso como "tipo desconhecido" (exceção) sem precisar de FK válida.
    public Guid? TipoAtoId { get; private set; }
    // Preenchido só quando TipoAtoId é nulo — o texto bruto que veio do relatório (RF-09/
    // seção 7 "Tipo desconhecido"). Sem isso não dá pra agrupar "quantas vezes 'X' apareceu
    // fora do catálogo" pra gerar a sugestão de aprendizado.
    public string? TipoAtoNomeOriginal { get; }
    // Quem produziu o ato (glossário, seção 2) — RF-14 (o card mostra escrevente/equipe) e
    // RF-38 (recalcular vencimento quando o prazo da equipe do escrevente muda) dependem disso.
    public Guid EscreventeId { get; private set; }
    public Etapa Etapa { get; private set; }
    public Prioridade Prioridade { get; private set; }
    // Instante do "andamento" que originou este registro (vem do relatório importado, não de
    // quando a importação rodou) — é o momentoDeReferencia usado pra calcular o vencimento, e
    // também a base da "linha de corte" que evita reimportar o que já foi processado.
    public DateTimeOffset AndamentoEm { get; }
    // Nulo quando o protocolo nasce fora de um lote (POST /protocolos/manual, por exemplo) —
    // RF-13 filtra "visão deste lote" por aqui.
    public Guid? LoteImportacaoId { get; }
    public Prazo? Prazo { get; private set; }
    public DateTimeOffset? VencimentoEm { get; private set; }
    public StatusProtocolo Status { get; private set; } = StatusProtocolo.Pool;
    public Guid? DonoId { get; private set; }
    public string? MotivoExcecao { get; private set; }
    public string? Observacao { get; private set; }
    public DateTimeOffset? IniciadoEm { get; private set; }
    public DateTimeOffset? ConcluidoEm { get; private set; }
    // RF-18a (linha do tempo do painel de detalhe): quando ganhou dono pela última vez — não
    // limpa ao voltar pro pool (EnviarParaPool), fica como histórico de "a última vez que foi
    // atribuído", já que o sistema ainda não tem um log de eventos de verdade (ver seção 8 do
    // documento de requisitos, "evento_decisao" — não construído).
    public DateTimeOffset? AtribuidoEm { get; private set; }
    // RNF-02 ("toda decisão automática registra a regra que a originou"): nulo quando a
    // atribuição não veio de uma regra específica (padrão aberto, ou decisão humana via
    // "pegar"/"atribuir manualmente"). Quando as decisões de etapa e de tipo vêm de regras
    // diferentes, guarda a de tipo — é a mais específica das duas (RF-31 fala de "tipos de ato"
    // como o alvo mais comum).
    public Guid? RegraAplicadaId { get; private set; }

    // RF-24a: quando o resultado foi trocado (Aprovado↔Reprovado) pelo próprio dono, dentro
    // da janela de correção. Não limpa em reabertura — é histórico de "já foi corrigido uma
    // vez", igual AtribuidoEm não limpa ao voltar pro pool.
    public DateTimeOffset? CorrigidoEm { get; private set; }
    // RF-24c: quando a distribuidora reabriu a conferência (via pedido aprovado ou ação
    // direta no painel de detalhe).
    public DateTimeOffset? ReabertoEm { get; private set; }

    // Pedido do dono ("a pessoa sai pra almoçar, por exemplo") — pausa sem contar o tempo
    // parado, mas sem devolver o ato pra fila (continua ocupando o limite de simultâneos,
    // RF-21: ela não pode iniciar outro enquanto este estiver pausado). Só tem valor enquanto
    // pausado; `Retomar` limpa de novo.
    public DateTimeOffset? PausadoEm { get; private set; }

    // Achado em uso real (produção): antes de existir isso, reabrir um protocolo já concluído
    // sobrescrevia `IniciadoEm`/`ConcluidoEm` na hora — a duração final só refletia o último
    // ciclo (ex.: 5 min da reabertura), perdendo o tempo da conferência original inteira. Um
    // registro por ciclo (não só a soma cega de um TimeSpan) porque o Dashboard (RF-43/45/46)
    // usa esse tempo pra medir carga/produtividade por pessoa — precisa saber QUEM fez cada
    // ciclo, não só quanto tempo, senão um ato reaberto e reatribuído (RF-24c, só quando o dono
    // original saiu da escala, RF-27) jogaria o tempo de uma pessoa na conta de outra.
    // `ReabrirConferencia` adiciona aqui o ciclo que está terminando, antes de zerar
    // `IniciadoEm`/`ConcluidoEm` pro próximo.
    private readonly List<CicloConferencia> _ciclosAnteriores = [];
    public IReadOnlyList<CicloConferencia> CiclosAnteriores => _ciclosAnteriores;

    // Achado numa conversa com o dono, pensando em uso real: nada garantia que "pausar" não
    // virasse um jeito de esconder tempo do tempo médio (usado numa conta de bonificação
    // externa, ver ObterDashboard.cs) — pausar "de mentira" bem na hora que o trabalho ficaria
    // lento. Decisão consciente: não bloquear nem limitar (time pequeno, confiança resolve),
    // só garantir que fica auditável — `Retomar` registra aqui cada pausa já encerrada (quando
    // começou, quando voltou), pro painel de detalhe poder mostrar "pausado Nx, M min no total".
    private readonly List<PausaConferencia> _pausas = [];
    public IReadOnlyList<PausaConferencia> Pausas => _pausas;

    // Pedido do dono ("como distribuidora e admin, quero editar o tempo de conferência de um
    // protocolo") — sobrescreve o valor calculado por CiclosAnteriores/IniciadoEm/ConcluidoEm
    // (não mexe nesses campos, só o que Duracao devolve). Guarda um histórico de ajustes (não
    // só o valor atual) pelo mesmo motivo de CiclosAnteriores/Pausas: auditoria (RNF-02) — esse
    // tempo alimenta uma conta real de bonificação (ver ObterDashboard.cs), não pode sumir quem
    // editou o quê.
    private readonly List<AjusteDeDuracao> _ajustesDeDuracao = [];
    public IReadOnlyList<AjusteDeDuracao> AjustesDeDuracao => _ajustesDeDuracao;
    private TimeSpan? DuracaoAjustada => _ajustesDeDuracao.Count > 0 ? _ajustesDeDuracao[^1].DuracaoNova : null;

    // RF-18i/j: só tem valor quando Status == Excluido — guarda o que era antes, pra
    // Restaurar() devolver exato (mesmo vencimento/dono/histórico, nada mais muda).
    public StatusProtocolo? StatusAntesDeExcluir { get; private set; }

    // A importação de lote (o fluxo real de entrada) nunca define prioridade alta — o
    // relatório do cartório não tem essa coluna. Isso é o único jeito de marcar um protocolo
    // como urgente na prática: uma decisão humana explícita da distribuidora, não um dado que
    // vem de algum lugar automaticamente.
    public void DefinirPrioridade(Prioridade prioridade) => Prioridade = prioridade;

    // RF-24: "duração" do ato — só existe depois de concluído. Soma o tempo do ciclo atual com
    // o de qualquer ciclo anterior já encerrado (CiclosAnteriores) — sem isso, um protocolo
    // reaberto mostraria só a duração da última rodada, escondendo o tempo real que ficou em
    // conferência desde o início. Um ajuste manual (AjustarDuracao) sobrescreve esse cálculo —
    // o valor mais recente em AjustesDeDuracao, se houver, sempre vence.
    public TimeSpan? Duracao => CalcularDuracao(DuracaoAjustada, IniciadoEm, ConcluidoEm, _ciclosAnteriores.Select(c => c.Duracao));

    // A mesma conta de Duracao sobre valores soltos — a mediana do tempo de referência (RF-46c) projeta
    // só estes campos do histórico de 12 meses em vez de materializar cada Protocolo (Infrastructure).
    public static TimeSpan? CalcularDuracao(
        TimeSpan? ultimoAjuste, DateTimeOffset? iniciadoEm, DateTimeOffset? concluidoEm, IEnumerable<TimeSpan> ciclosAnteriores) =>
        ultimoAjuste ?? (iniciadoEm is { } inicio && concluidoEm is { } fim
            ? ciclosAnteriores.Aggregate(fim - inicio, (soma, ciclo) => soma + ciclo)
            : null);

    // ADR-0032/0035: o tempo deste ato repartido por quem o conferiu — um item por ciclo já encerrado
    // (CiclosAnteriores, cada um de quem o fez) mais o ciclo final (do dono atual). Com ajuste manual, o
    // valor corrigido substitui a conta inteira e vai todo pro dono atual. Base do tempo médio por
    // conferente e do ritmo (RF-46a) no Dashboard.
    public IReadOnlyList<(Guid ConferenteId, TimeSpan Duracao)> TemposPorConferente()
    {
        if (_ajustesDeDuracao.Count > 0)
        {
            return DonoId is { } donoAjustado && Duracao is { } duracaoAjustada ? [(donoAjustado, duracaoAjustada)] : [];
        }

        var tempos = _ciclosAnteriores.Select(c => (c.ConferenteId, c.Duracao)).ToList();
        if (DonoId is { } dono && IniciadoEm is { } inicio && ConcluidoEm is { } fim)
        {
            tempos.Add((dono, fim - inicio));
        }

        return tempos;
    }

    // Soma dos pedaços deste ato que foram de `conferenteId`; nulo se nenhum foi.
    public TimeSpan? TempoDe(Guid conferenteId)
    {
        var dele = TemposPorConferente().Where(t => t.ConferenteId == conferenteId).ToList();
        return dele.Count == 0 ? null : dele.Aggregate(TimeSpan.Zero, (soma, t) => soma + t.Duracao);
    }

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

    public void AtribuirA(Guid conferenteId, DateTimeOffset agora, Guid? regraAplicadaId = null)
    {
        Status = StatusProtocolo.Atribuido;
        DonoId = conferenteId;
        MotivoExcecao = null;
        AtribuidoEm = agora;
        RegraAplicadaId = regraAplicadaId;
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

    // RF-18g: troca as 3 identidades que definem prazo/alçada — quem decide se isso muda o
    // vencimento (recalcular a partir de AndamentoEm) e se o dono perde alçada (RF-18h) é o
    // caso de uso, não o Domain; aqui é só a mutação dos dados.
    public void EditarDadosBasicos(Guid? tipoAtoId, Guid escreventeId, Etapa etapa)
    {
        TipoAtoId = tipoAtoId;
        EscreventeId = escreventeId;
        Etapa = etapa;
    }

    // RF-18i: soft-delete — guarda o status atual pra Restaurar() devolver exato. Privilégio
    // da distribuidora, front confirma antes (RF-18i pede diálogo de confirmação).
    public void Excluir()
    {
        StatusAntesDeExcluir = Status;
        Status = StatusProtocolo.Excluido;
    }

    // RF-18j: desfazer — mesmo vencimento, dono e histórico, porque nada além de Status foi
    // tocado por Excluir(). Caller garante que só é chamado quando Status == Excluido.
    public void Restaurar()
    {
        Status = StatusAntesDeExcluir!.Value;
        StatusAntesDeExcluir = null;
    }

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

    // RF-24a: troca o resultado — quem decide se está dentro da janela de 15 min e se é o
    // dono é o caso de uso (CorrigirResultado), aqui só a transição em si. Permite corrigir
    // mais de uma vez dentro da janela (o protótipo aprovado não impede, e o requisito não
    // proíbe) — CorrigidoEm sempre reflete a correção mais recente.
    public void CorrigirResultado(DateTimeOffset agora)
    {
        Status = Status == StatusProtocolo.Aprovado ? StatusProtocolo.Reprovado : StatusProtocolo.Aprovado;
        CorrigidoEm = agora;
    }

    // RF-24c: reabertura — mesmo dono (DonoId não muda), devolve pra Atribuído (fila da
    // pessoa), não direto pra Conferindo — achado em uso real (produção, protocolo 263605):
    // reabrir já ligava o cronômetro na hora, sem a pessoa ter clicado em nada, o que também
    // fazia o ato pular direto pra "Em conferência" em vez de aparecer nas atribuídas dela.
    // Cronômetro só volta a andar quando ela chamar IniciarConferencia de novo, igual a
    // primeira vez. ConcluidoEm volta a nulo porque o ato deixou de estar concluído (Duracao
    // volta a não existir até uma nova conclusão) — usada tanto pelo pedido aprovado quanto
    // pela ação direta "reabrir conferência" no painel de detalhe.
    public void ReabrirConferencia(DateTimeOffset agora)
    {
        // Registra o ciclo que está terminando agora, com quem foi o dono dele, antes de zerar
        // IniciadoEm/ConcluidoEm — sem isso, o tempo da conferência original (antes desta
        // reabertura) desaparecia da Duracao final, e o Dashboard não teria como saber de quem
        // foi esse tempo se o protocolo for reatribuído depois.
        if (IniciadoEm is { } inicioDoCiclo && ConcluidoEm is { } fimDoCiclo && DonoId is { } donoDoCiclo)
        {
            _ciclosAnteriores.Add(new CicloConferencia(donoDoCiclo, inicioDoCiclo, fimDoCiclo));
        }

        Status = StatusProtocolo.Atribuido;
        IniciadoEm = null;
        ConcluidoEm = null;
        ReabertoEm = agora;

        // Achado em uso real (produção): o vencimento continuava calculado a partir da entrada
        // original — um ato reaberto dias depois aparecia "vencido há Xd" na hora, mesmo sendo
        // uma conferência nova pedida agora. AndamentoEm (a "entrada", histórico de quando o ato
        // chegou pela primeira vez) não muda — só o vencimento recalcula como um prazo novo, do
        // mesmo tipo (TipoPrazo) que o protocolo já tinha, a partir do momento da reabertura.
        if (Prazo is { } prazoAtual)
        {
            DefinirPrazo(prazoAtual, agora);
        }
    }

    // Pausa — mesmo mecanismo de fechar ciclo que ReabrirConferencia já usa (fecha o pedaço que
    // estava rodando, guarda de quem foi, pra Duracao final continuar somando certo), mas
    // **sem** sair de Conferindo nem mudar Status/DonoId: pausar não é devolver o ato, é só
    // congelar o cronômetro do mesmo ciclo — continua contando pro limite de simultâneos
    // (RF-21), a pessoa não pode começar outro enquanto este está pausado.
    public void Pausar(DateTimeOffset agora)
    {
        if (IniciadoEm is { } inicioDoCiclo && DonoId is { } donoDoCiclo)
        {
            _ciclosAnteriores.Add(new CicloConferencia(donoDoCiclo, inicioDoCiclo, agora));
        }

        IniciadoEm = null;
        PausadoEm = agora;
    }

    // Volta a contar o tempo — abre um ciclo novo a partir de agora, igual IniciarConferencia
    // faz na primeira vez (Status já continuava Conferindo durante a pausa, não muda aqui).
    public void Retomar(DateTimeOffset agora)
    {
        if (PausadoEm is { } pausadoEm)
        {
            _pausas.Add(new PausaConferencia(pausadoEm, agora));
        }

        IniciadoEm = agora;
        PausadoEm = null;
    }

    // Pedido do dono: distribuidora (admin) corrige o tempo final de um protocolo já concluído.
    // Não mexe em IniciadoEm/ConcluidoEm/CiclosAnteriores — só sobrescreve o que Duracao
    // devolve, guardando o ajuste no histórico (quem, quando, valor anterior/novo, motivo).
    // Quem decide QUANDO isso é permitido (só Aprovado/Reprovado) é o caso de uso, mesmo
    // padrão já usado em ReabrirConferencia/IniciarConferencia — aqui é só a mutação.
    public void AjustarDuracao(TimeSpan duracaoNova, Guid ajustadoPorId, DateTimeOffset agora, string? motivo)
    {
        _ajustesDeDuracao.Add(new AjusteDeDuracao(ajustadoPorId, agora, Duracao, duracaoNova, motivo));
    }
}
