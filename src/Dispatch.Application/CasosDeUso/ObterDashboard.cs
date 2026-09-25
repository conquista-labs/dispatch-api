using Dispatch.Domain;

namespace Dispatch.Application;

// RF-42-46: score = 40% volume + 30% prazo + 20% qualidade + 10% complexidade — fórmula do
// protótipo aprovado (o documento de requisitos só nomeia os 4 fatores e os pesos, não define
// a fórmula matemática exata). Volume e complexidade são normalizados pelo máximo do grupo no
// período (decisão do protótipo, não do requisito); prazo e qualidade já são frações diretas.
// "Aprovados" (e a parcela de qualidade do score) usa o resultado ATUAL (Status == Aprovado) de
// todos os concluídos. "Aprovados na 1ª" (RF-43, decisão 3 do dono) é outro número, só informativo
// por enquanto: das conferências de 1ª rodada (RF-24k, NumeroDaConferencia == 1) quantas estão
// aprovadas — correção reprovado→aprovado conta, porque vale o resultado atual da linha.
//
// Período por calendário no dia de Brasília (CalendarioDoPeriodo, ADR-0041), com o mesmo trecho do
// período anterior pra variação (RF-42b) e a série por dia útil/semana (RF-42c).
public sealed class ObterDashboard(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    ITipoAtoRepository tiposAto,
    IEscreventeRepository escreventes,
    IEquipeRepository equipes,
    IUsuarioRepository usuarios,
    IRelogio relogio)
{
    private const int ScoreIntegral = 85;
    private const int ScoreParcial = 70;

    // `incluirAvaliacaoDePessoal` (perfil Administrador, ADR-0039, RF-43a): nível, score, faixa e
    // parcelas só saem pra um token de Administrador. O default é fechado — quem não passa a flag
    // não vê. Sem ela, a gestão recebe a lista ordenada por NOME: a ordem por score, sozinha, já
    // entregaria o ranking mesmo com o campo nulo.
    public async Task<ResultadoDashboard> ExecutarAsync(
        PeriodoDashboard periodo,
        Guid? conferenteRestritoId,
        bool incluirAvaliacaoDePessoal = false,
        CancellationToken cancellationToken = default)
    {
        var agora = relogio.Agora;
        var intervalo = CalendarioDoPeriodo.Atual(periodo, agora);
        var intervaloAnterior = CalendarioDoPeriodo.MesmoTrechoAnterior(periodo, agora);

        // Duas buscas pela mesma consulta indexada (status, concluido_em), cada uma só com o seu
        // trecho — uma busca única de inicioAnterior até agora traria de graça o vão entre o fim do
        // trecho anterior e o início do atual (no dia 15, meio mês que ninguém usa).
        var concluidosNoPeriodo = await protocolos.ObterConcluidosNoPeriodoAsync(intervalo.Inicio, intervalo.Fim, cancellationToken);
        var concluidosNoTrechoAnterior = await protocolos.ObterConcluidosNoPeriodoAsync(
            intervaloAnterior.Inicio, intervaloAnterior.Fim, cancellationToken);
        // Na visão restrita, o trecho anterior só serve aos KPIs dele (RF-45).
        IReadOnlyCollection<Protocolo> anteriorDoRecorte = conferenteRestritoId is { } restritoAnterior
            ? concluidosNoTrechoAnterior.Where(p => p.DonoId == restritoAnterior).ToList()
            : concluidosNoTrechoAnterior;

        // RF-24k em lote pros dois trechos: uma query pelos números distintos (ADR-0038), não uma por
        // protocolo. Só serve ao "aprovado na 1ª".
        var numeroDaConferencia = await NumeroDaConferenciaEmLote.CalcularAsync(
            protocolos, [.. concluidosNoPeriodo, .. anteriorDoRecorte], cancellationToken);

        var catalogoTipos = (await tiposAto.ObterTodosAsync(cancellationToken)).ToDictionary(t => t.Id);
        var todosConferentes = (await conferentes.ObterTodosAsync(cancellationToken)).ToDictionary(c => c.Id);
        var usuarioPorId = (await usuarios.ObterVariosPorIdsAsync(
                todosConferentes.Values.Select(c => c.UsuarioId).ToList(), cancellationToken))
            .ToDictionary(u => u.Id);

        var porDono = concluidosNoPeriodo
            .Where(p => p.DonoId is not null && todosConferentes.ContainsKey(p.DonoId.Value))
            .GroupBy(p => p.DonoId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Tempo de conferência por CICLO, não por protocolo — RF-43/45/46 usam esse tempo pra
        // medir carga/produtividade de cada pessoa (achado em uso real, protocolo 263605): um
        // ato reaberto e reatribuído (RF-24c, só quando o dono original saiu da escala, RF-27)
        // não pode fazer a pessoa nova herdar o tempo que a pessoa antiga já gastou nele. Cada
        // CicloConferencia sabe de quem foi; o ciclo final/atual (ainda em Protocolo, não em
        // CiclosAnteriores) é sempre do DonoId de agora. Só usado pro TempoMedio de "por
        // conferente" — Volume/Score/PercentualNoPrazo/PercentualAprovado continuam do jeito
        // que já eram, atribuídos ao dono atual do protocolo inteiro (não foi pedido pra mudar
        // isso, só o tempo).
        var temposPorConferente = ConstruirTemposPorConferente(concluidosNoPeriodo);

        // RF-45: os KPIs do topo também são "os números dele", não o total da operação — sem
        // isso, um conferente via "atos conferidos" contando o trabalho de todo mundo, com a
        // linha de desempenho logo abaixo mostrando só a dele (achado real: os dois pareciam
        // dados desencontrados, cada um lendo uma fonte diferente).
        var kpis = conferenteRestritoId is { } idRestrito
            ? CalcularKpis(porDono.GetValueOrDefault(idRestrito, []), temposPorConferente.GetValueOrDefault(idRestrito, []), numeroDaConferencia)
            : CalcularKpis(concluidosNoPeriodo, temposProprios: null, numeroDaConferencia);

        // RF-42b: mesma conta sobre o mesmo trecho do período anterior — na visão restrita também,
        // com os números do próprio conferente (tempo pelos ciclos dele, como no período atual).
        var kpisAnterior = conferenteRestritoId is { } idRestritoAnterior
            ? CalcularKpis(
                anteriorDoRecorte,
                ConstruirTemposPorConferente(concluidosNoTrechoAnterior).GetValueOrDefault(idRestritoAnterior, []),
                numeroDaConferencia)
            : CalcularKpis(concluidosNoTrechoAnterior, temposProprios: null, numeroDaConferencia);

        // RF-42c: série do próprio conferente na visão restrita (mesmo recorte dos KPIs dele).
        var baseDaSerie = conferenteRestritoId is { } idRestritoSerie ? porDono.GetValueOrDefault(idRestritoSerie, []) : concluidosNoPeriodo;
        var serie = SerieDoPeriodo.Montar(
            periodo, agora, baseDaSerie.Select(p => new ConclusaoNaSerie(p.ConcluidoEm!.Value, Estourado: !EstaNoPrazo(p))));

        var maxVolume = porDono.Count == 0 ? 0 : porDono.Values.Max(lista => lista.Count);
        var maxComplexidadeMedia = porDono.Count == 0
            ? 0
            : porDono.Values.Max(lista => ComplexidadeMedia(lista, catalogoTipos));

        // Une quem é dono de algum protocolo concluído no período com quem só aparece em
        // temposPorConferente (fez um ciclo, mas o protocolo foi reatribuído antes de concluir
        // de vez) — sem isso, o tempo de quem só teve um ciclo intermediário nunca apareceria em
        // lugar nenhum do Dashboard, mesmo tendo de fato trabalhado (achado pensando no caso do
        // 263605: a Ana fez os primeiros 20 min, saiu da escala, o Bruno terminou — sem essa
        // união, os 20 min da Ana desapareceriam do relatório de produtividade).
        var idsComAtividadeNoPeriodo = porDono.Keys.Union(temposPorConferente.Keys).Where(todosConferentes.ContainsKey).ToList();

        var todosOsDesempenhos = idsComAtividadeNoPeriodo
            .Select(id => CalcularDesempenho(
                id, todosConferentes[id], usuarioPorId.GetValueOrDefault(todosConferentes[id].UsuarioId),
                porDono.GetValueOrDefault(id, []), temposPorConferente.GetValueOrDefault(id, []), catalogoTipos, maxVolume,
                maxComplexidadeMedia, numeroDaConferencia, mostrarFaixa: conferenteRestritoId is null))
            .OrderByDescending(d => d.Score)
            .ToList();

        var porTipoAto = conferenteRestritoId is null ? CalcularPorTipoAto(concluidosNoPeriodo, catalogoTipos) : [];

        if (conferenteRestritoId is null)
        {
            var todosEscreventes = (await escreventes.ObterTodosAsync(cancellationToken)).ToDictionary(e => e.Id);
            var todasEquipes = (await equipes.ObterTodasAsync(cancellationToken)).ToDictionary(e => e.Id);
            var cumprimentoPrazoEquipe = CalcularCumprimentoPrazoPorEquipe(concluidosNoPeriodo, todosEscreventes, todasEquipes);
            var desempenhoDaGestao = incluirAvaliacaoDePessoal
                ? todosOsDesempenhos
                : todosOsDesempenhos
                    .Select(SemAvaliacaoDePessoal)
                    .OrderBy(d => d.Nome, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(d => d.ConferenteId)
                    .ToList();
            return new ResultadoDashboard(
                intervalo.Inicio, intervalo.Fim, kpis, kpisAnterior, serie, desempenhoDaGestao, MediaDaCasa: null, porTipoAto,
                cumprimentoPrazoEquipe);
        }

        // RF-45: o conferente só vê os próprios números + a média da casa sem identificar
        // ninguém — nunca a lista completa com nomes de colegas.
        // O próprio score e as parcelas continuam (RF-45 pede); o próprio nível não (ADR-0039: o
        // cargo só sai pra Administrador). A média da casa é calculada antes, sobre a lista cheia.
        var meuDesempenho = todosOsDesempenhos.SingleOrDefault(d => d.ConferenteId == conferenteRestritoId);
        var lista = meuDesempenho is null ? [] : (IReadOnlyList<DesempenhoConferente>)[meuDesempenho with { Nivel = null }];
        var mediaDaCasa = CalcularMediaDaCasa(todosOsDesempenhos);
        return new ResultadoDashboard(
            intervalo.Inicio, intervalo.Fim, kpis, kpisAnterior, serie, lista, mediaDaCasa, PorTipoAto: [], CumprimentoPrazoEquipe: []);
    }

    private static DesempenhoConferente SemAvaliacaoDePessoal(DesempenhoConferente d) =>
        d with { Nivel = null, Score = null, Faixa = null, Parcelas = null };

    // `temposProprios`: nulo pra visão agregada (o "tempo médio da operação" continua somando o
    // protocolo inteiro, do início ao fim, não importa quantas pessoas passaram por ele — é
    // "quanto tempo esse ato leva", não "quanto tempo essa pessoa trabalhou"); uma lista (mesmo
    // vazia) pra visão restrita de um conferente (RF-45: "os números dele" têm que refletir só
    // os ciclos que ele mesmo fez, ver ConstruirTemposPorConferente).
    private static KpisDashboard CalcularKpis(
        IReadOnlyCollection<Protocolo> concluidos, IReadOnlyCollection<TimeSpan>? temposProprios,
        IReadOnlyDictionary<Guid, int> numeroDaConferencia)
    {
        if (concluidos.Count == 0)
        {
            return new KpisDashboard(0, 0, 0, PercentualAprovadoNaPrimeira: null, TempoMedio: null);
        }

        var noPrazo = concluidos.Count(EstaNoPrazo);
        var aprovados = concluidos.Count(p => p.Status == StatusProtocolo.Aprovado);
        var duracoes = temposProprios ?? concluidos.Select(p => p.Duracao).Where(d => d is not null).Select(d => d!.Value).ToList();
        TimeSpan? tempoMedio = duracoes.Count > 0
            ? TimeSpan.FromTicks((long)duracoes.Average(d => d.Ticks))
            : null;

        return new KpisDashboard(
            concluidos.Count,
            (double)noPrazo / concluidos.Count,
            (double)aprovados / concluidos.Count,
            PercentualAprovadoNaPrimeira(concluidos, numeroDaConferencia),
            tempoMedio);
    }

    // RF-43 "aprovados na 1ª" (decisão 3 do dono): das linhas concluídas que são a 1ª conferência
    // daquele Número+etapa (RF-24k), a fração com status Aprovado agora. Nulo sem nenhuma 1ª
    // conferência — "0%" diria que todas voltaram. Id fora do dicionário = 1ª (o default seguro de
    // NumeroDaConferenciaEmLote).
    private static double? PercentualAprovadoNaPrimeira(
        IReadOnlyCollection<Protocolo> concluidos, IReadOnlyDictionary<Guid, int> numeroDaConferencia)
    {
        var primeiras = concluidos.Where(p => numeroDaConferencia.GetValueOrDefault(p.Id, 1) == 1).ToList();
        return primeiras.Count == 0
            ? null
            : (double)primeiras.Count(p => p.Status == StatusProtocolo.Aprovado) / primeiras.Count;
    }

    // Achata cada protocolo concluído em (quem, quanto tempo) por ciclo — um ciclo por
    // CicloConferencia já encerrado (reabertura), mais o ciclo final/atual (o que está direto em
    // Protocolo.IniciadoEm/ConcluidoEm/DonoId, sempre do dono de agora). Um protocolo nunca
    // reaberto vira só uma entrada (o comportamento de sempre); um reaberto duas vezes vira até
    // três, cada uma na conta de quem de fato conferiu aquele pedaço.
    private static Dictionary<Guid, List<TimeSpan>> ConstruirTemposPorConferente(IReadOnlyCollection<Protocolo> concluidos)
    {
        var porConferente = new Dictionary<Guid, List<TimeSpan>>();

        void Adiciona(Guid conferenteId, TimeSpan duracao)
        {
            if (!porConferente.TryGetValue(conferenteId, out var lista))
            {
                lista = [];
                porConferente[conferenteId] = lista;
            }

            lista.Add(duracao);
        }

        foreach (var protocolo in concluidos)
        {
            // Ajuste manual (pedido do dono: distribuidora corrige o tempo final de um
            // protocolo) substitui a conta por ciclo inteira — o valor corrigido vai inteiro
            // pro dono atual, não fica misturado com os pedaços "originais" que a própria
            // correção considerou errados.
            if (protocolo.AjustesDeDuracao.Count > 0)
            {
                if (protocolo.DonoId is { } donoAjustado && protocolo.Duracao is { } duracaoAjustada)
                {
                    Adiciona(donoAjustado, duracaoAjustada);
                }

                continue;
            }

            foreach (var ciclo in protocolo.CiclosAnteriores)
            {
                Adiciona(ciclo.ConferenteId, ciclo.Duracao);
            }

            if (protocolo.DonoId is { } donoId && protocolo.IniciadoEm is { } inicio && protocolo.ConcluidoEm is { } fim)
            {
                Adiciona(donoId, fim - inicio);
            }
        }

        return porConferente;
    }

    private static bool EstaNoPrazo(Protocolo p) => p.VencimentoEm is null || p.ConcluidoEm is null || p.ConcluidoEm <= p.VencimentoEm;

    private static double ComplexidadeMedia(IReadOnlyCollection<Protocolo> protocolosDoConferente, IReadOnlyDictionary<Guid, TipoAto> catalogo)
    {
        var pesos = protocolosDoConferente
            .Where(p => p.TipoAtoId is not null && catalogo.ContainsKey(p.TipoAtoId.Value))
            .Select(p => catalogo[p.TipoAtoId!.Value].PesoComplexidade)
            .ToList();
        return pesos.Count == 0 ? 0 : pesos.Average();
    }

    private static DesempenhoConferente CalcularDesempenho(
        Guid conferenteId, Conferente conferente, Usuario? usuario, IReadOnlyCollection<Protocolo> protocolosDoConferente,
        IReadOnlyCollection<TimeSpan> temposProprios,
        IReadOnlyDictionary<Guid, TipoAto> catalogo, int maxVolume, double maxComplexidadeMedia,
        IReadOnlyDictionary<Guid, int> numeroDaConferencia, bool mostrarFaixa)
    {
        var volume = protocolosDoConferente.Count;
        var noPrazo = protocolosDoConferente.Count(EstaNoPrazo);
        var aprovados = protocolosDoConferente.Count(p => p.Status == StatusProtocolo.Aprovado);
        // TempoMedio vem dos ciclos que esta pessoa de fato conferiu (ConstruirTemposPorConferente),
        // não de Protocolo.Duracao — um protocolo que ela só herdou depois de uma reabertura
        // (RF-24c) carrega tempo de ciclos anteriores que não são dela.
        TimeSpan? tempoMedio = temposProprios.Count > 0 ? TimeSpan.FromTicks((long)temposProprios.Average(d => d.Ticks)) : null;
        var complexidadeMedia = ComplexidadeMedia(protocolosDoConferente, catalogo);

        var pctNoPrazo = volume == 0 ? 0 : (double)noPrazo / volume;
        var pctAprovado = volume == 0 ? 0 : (double)aprovados / volume;

        var pontosVolume = maxVolume == 0 ? 0 : 40.0 * volume / maxVolume;
        var pontosPrazo = 30.0 * pctNoPrazo;
        var pontosQualidade = 20.0 * pctAprovado;
        var pontosComplexidade = maxComplexidadeMedia == 0 ? 0 : 10.0 * complexidadeMedia / maxComplexidadeMedia;

        var score = (int)Math.Round(pontosVolume + pontosPrazo + pontosQualidade + pontosComplexidade);
        var faixa = mostrarFaixa
            ? score >= ScoreIntegral ? FaixaBonificacao.Integral : score >= ScoreParcial ? FaixaBonificacao.Parcial : FaixaBonificacao.Fora
            : (FaixaBonificacao?)null;

        return new DesempenhoConferente(
            conferenteId, usuario?.Nome ?? "—", conferente.Nivel, volume, tempoMedio, pctNoPrazo, pctAprovado,
            PercentualAprovadoNaPrimeira(protocolosDoConferente, numeroDaConferencia), complexidadeMedia, score, faixa, new ParcelasScore(pontosVolume, pontosPrazo, pontosQualidade, pontosComplexidade));
    }

    // RF-45: linha de comparação sem identificar ninguém — média simples entre quem teve
    // volume no período (parado em 0 não entraria na média de quem trabalhou).
    private static DesempenhoConferente? CalcularMediaDaCasa(IReadOnlyList<DesempenhoConferente> todos)
    {
        var comVolume = todos.Where(d => d.Volume > 0).ToList();
        if (comVolume.Count == 0)
        {
            return null;
        }

        var duracoesMedias = comVolume.Where(d => d.TempoMedio is not null).Select(d => d.TempoMedio!.Value).ToList();
        TimeSpan? tempoMedio = duracoesMedias.Count > 0 ? TimeSpan.FromTicks((long)duracoesMedias.Average(d => d.Ticks)) : null;
        // Mesma média simples entre pessoas, só entre quem teve alguma 1ª conferência (null não é 0%).
        var aprovadosNaPrimeira = comVolume.Where(d => d.PercentualAprovadoNaPrimeira is not null)
            .Select(d => d.PercentualAprovadoNaPrimeira!.Value).ToList();

        return new DesempenhoConferente(
            ConferenteId: Guid.Empty,
            Nome: null,
            Nivel: null,
            Volume: (int)Math.Round(comVolume.Average(d => d.Volume)),
            TempoMedio: tempoMedio,
            PercentualNoPrazo: comVolume.Average(d => d.PercentualNoPrazo),
            PercentualAprovado: comVolume.Average(d => d.PercentualAprovado),
            PercentualAprovadoNaPrimeira: aprovadosNaPrimeira.Count > 0 ? aprovadosNaPrimeira.Average() : null,
            ComplexidadeMedia: comVolume.Average(d => d.ComplexidadeMedia),
            Score: (int)Math.Round(comVolume.Average(d => d.Score ?? 0)),
            Faixa: null,
            Parcelas: null);
    }

    private static IReadOnlyList<DesempenhoTipoAto> CalcularPorTipoAto(
        IReadOnlyCollection<Protocolo> concluidos, IReadOnlyDictionary<Guid, TipoAto> catalogo) =>
        concluidos
            .Where(p => p.TipoAtoId is not null && catalogo.ContainsKey(p.TipoAtoId.Value))
            .GroupBy(p => p.TipoAtoId!.Value)
            .Select(g =>
            {
                var duracoes = g.Select(p => p.Duracao).Where(d => d is not null).Select(d => d!.Value).ToList();
                TimeSpan? tempoMedio = duracoes.Count > 0 ? TimeSpan.FromTicks((long)duracoes.Average(d => d.Ticks)) : null;
                var reprovados = g.Count(p => p.Status == StatusProtocolo.Reprovado);
                return new DesempenhoTipoAto(g.Key, catalogo[g.Key].Nome, g.Count(), tempoMedio, (double)reprovados / g.Count());
            })
            .OrderByDescending(t => t.Volume)
            .ToList();

    // RF-43: "onde o prazo combinado não está sendo cumprido" — agrupa por equipe do escrevente
    // (protótipo aprovado, `slaEquipes`) + etapa, já que o prazo combinado é por essa dupla
    // (Equipe.PrazoPara(Etapa)), não só por equipe. Escrevente sem equipe entra como grupo
    // próprio ("sem equipe", EquipeId nulo) — ele tem prazo real (D+1 padrão, ver
    // ResolvedorDePrazo), só não tem equipe pra nomear. Pior percentual primeiro, igual o
    // protótipo (`sort((a,b) => a.noPrazo - b.noPrazo)`).
    private static IReadOnlyList<CumprimentoPrazoEquipe> CalcularCumprimentoPrazoPorEquipe(
        IReadOnlyCollection<Protocolo> concluidos,
        IReadOnlyDictionary<Guid, Escrevente> catalogoEscreventes,
        IReadOnlyDictionary<Guid, Equipe> catalogoEquipes) =>
        concluidos
            .Where(p => catalogoEscreventes.ContainsKey(p.EscreventeId))
            .GroupBy(p => (EquipeId: catalogoEscreventes[p.EscreventeId].EquipeId, p.Etapa))
            .Select(g =>
            {
                var equipeNome = g.Key.EquipeId is { } equipeId && catalogoEquipes.TryGetValue(equipeId, out var equipe)
                    ? equipe.Nome
                    : "sem equipe";
                var noPrazo = g.Count(EstaNoPrazo);
                return new CumprimentoPrazoEquipe(
                    g.Key.EquipeId, equipeNome, g.Key.Etapa, g.First().Prazo?.Tipo, g.Count(), (double)noPrazo / g.Count());
            })
            .OrderBy(c => c.PercentualNoPrazo)
            .ToList();
}

public sealed record ResultadoDashboard(
    DateTimeOffset PeriodoInicio,
    DateTimeOffset PeriodoFim,
    KpisDashboard Kpis,
    KpisDashboard KpisAnterior,
    SerieDoPeriodo Serie,
    IReadOnlyList<DesempenhoConferente> Desempenho,
    DesempenhoConferente? MediaDaCasa,
    IReadOnlyList<DesempenhoTipoAto> PorTipoAto,
    IReadOnlyList<CumprimentoPrazoEquipe> CumprimentoPrazoEquipe);

public sealed record KpisDashboard(
    int AtosConferidos, double PercentualNoPrazo, double PercentualAprovado, double? PercentualAprovadoNaPrimeira, TimeSpan? TempoMedio);

public sealed record DesempenhoConferente(
    Guid ConferenteId,
    string? Nome,
    Nivel? Nivel,
    int Volume,
    TimeSpan? TempoMedio,
    double PercentualNoPrazo,
    double PercentualAprovado,
    // Nulo sem nenhuma 1ª conferência (RF-24k) entre os protocolos do conferente no período.
    double? PercentualAprovadoNaPrimeira,
    double ComplexidadeMedia,
    // Nulo pra quem não é Administrador (ADR-0039).
    int? Score,
    FaixaBonificacao? Faixa,
    ParcelasScore? Parcelas);

// Pontos já ponderados (sobre 40/30/20/10), não percentuais crus — o front mostra "32.4 / 40" direto.
public sealed record ParcelasScore(double Volume, double Prazo, double Qualidade, double Complexidade);

public enum FaixaBonificacao
{
    Integral,
    Parcial,
    Fora
}

public sealed record DesempenhoTipoAto(Guid TipoAtoId, string Nome, int Volume, TimeSpan? TempoMedio, double PercentualReprovacao);

// EquipeId nulo = "sem equipe" (EquipeNome já vem como "sem equipe" nesse caso — mesmo padrão
// de InfoProtocolo no front, mas resolvido aqui porque é o único lugar do Dashboard que precisa
// desse nome). Prazo nulo só pode acontecer se, por algum motivo, nenhum protocolo do grupo
// tiver prazo definido (não deveria acontecer com protocolo concluído, mas o vencimento em si
// não depende disso — é só o texto informativo "etapa · prazo" que ficaria incompleto).
public sealed record CumprimentoPrazoEquipe(
    Guid? EquipeId, string EquipeNome, Etapa Etapa, TipoPrazo? Prazo, int Total, double PercentualNoPrazo);
