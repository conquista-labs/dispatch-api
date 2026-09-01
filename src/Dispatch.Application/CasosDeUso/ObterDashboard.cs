using Dispatch.Domain;

namespace Dispatch.Application;

// RF-42-46: score = 40% volume + 30% prazo + 20% qualidade + 10% complexidade — fórmula do
// protótipo aprovado (o documento de requisitos só nomeia os 4 fatores e os pesos, não define
// a fórmula matemática exata). Volume e complexidade são normalizados pelo máximo do grupo no
// período (decisão do protótipo, não do requisito); prazo e qualidade já são frações diretas.
// "Aprovados" usa o resultado ATUAL (Status == Aprovado), não "aprovado na 1ª vez" — não existe
// histórico do resultado original antes de uma correção (RF-24a) salvo à parte; documentado
// como simplificação consciente.
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

    public async Task<ResultadoDashboard> ExecutarAsync(
        PeriodoDashboard periodo, Guid? conferenteRestritoId, CancellationToken cancellationToken = default)
    {
        var agora = relogio.Agora;
        var desde = agora.AddDays(-DiasDoPeriodo(periodo));

        var concluidosNoPeriodo = await protocolos.ObterConcluidosNoPeriodoAsync(desde, agora, cancellationToken);
        var catalogoTipos = (await tiposAto.ObterTodosAsync(cancellationToken)).ToDictionary(t => t.Id);
        var todosConferentes = (await conferentes.ObterTodosAsync(cancellationToken)).ToDictionary(c => c.Id);
        var usuarioPorId = (await usuarios.ObterVariosPorIdsAsync(
                todosConferentes.Values.Select(c => c.UsuarioId).ToList(), cancellationToken))
            .ToDictionary(u => u.Id);

        var porDono = concluidosNoPeriodo
            .Where(p => p.DonoId is not null && todosConferentes.ContainsKey(p.DonoId.Value))
            .GroupBy(p => p.DonoId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var kpis = CalcularKpis(concluidosNoPeriodo);

        var maxVolume = porDono.Count == 0 ? 0 : porDono.Values.Max(lista => lista.Count);
        var maxComplexidadeMedia = porDono.Count == 0
            ? 0
            : porDono.Values.Max(lista => ComplexidadeMedia(lista, catalogoTipos));

        var todosOsDesempenhos = porDono
            .Select(par => CalcularDesempenho(
                par.Key, todosConferentes[par.Key], usuarioPorId.GetValueOrDefault(todosConferentes[par.Key].UsuarioId),
                par.Value, catalogoTipos, maxVolume, maxComplexidadeMedia, mostrarFaixa: conferenteRestritoId is null))
            .OrderByDescending(d => d.Score)
            .ToList();

        var porTipoAto = conferenteRestritoId is null ? CalcularPorTipoAto(concluidosNoPeriodo, catalogoTipos) : [];

        if (conferenteRestritoId is null)
        {
            var todosEscreventes = (await escreventes.ObterTodosAsync(cancellationToken)).ToDictionary(e => e.Id);
            var todasEquipes = (await equipes.ObterTodasAsync(cancellationToken)).ToDictionary(e => e.Id);
            var cumprimentoPrazoEquipe = CalcularCumprimentoPrazoPorEquipe(concluidosNoPeriodo, todosEscreventes, todasEquipes);
            return new ResultadoDashboard(kpis, todosOsDesempenhos, MediaDaCasa: null, porTipoAto, cumprimentoPrazoEquipe);
        }

        // RF-45: o conferente só vê os próprios números + a média da casa sem identificar
        // ninguém — nunca a lista completa com nomes de colegas.
        var meuDesempenho = todosOsDesempenhos.SingleOrDefault(d => d.ConferenteId == conferenteRestritoId);
        var lista = meuDesempenho is null ? [] : (IReadOnlyList<DesempenhoConferente>)[meuDesempenho];
        var mediaDaCasa = CalcularMediaDaCasa(todosOsDesempenhos);
        return new ResultadoDashboard(kpis, lista, mediaDaCasa, PorTipoAto: [], CumprimentoPrazoEquipe: []);
    }

    private static int DiasDoPeriodo(PeriodoDashboard periodo) => periodo switch
    {
        PeriodoDashboard.Semana => 7,
        PeriodoDashboard.Mes => 30,
        PeriodoDashboard.Trimestre => 90,
        _ => throw new ArgumentOutOfRangeException(nameof(periodo), periodo, message: null)
    };

    private static KpisDashboard CalcularKpis(IReadOnlyCollection<Protocolo> concluidos)
    {
        if (concluidos.Count == 0)
        {
            return new KpisDashboard(0, 0, 0, null);
        }

        var noPrazo = concluidos.Count(EstaNoPrazo);
        var aprovados = concluidos.Count(p => p.Status == StatusProtocolo.Aprovado);
        var duracoes = concluidos.Select(p => p.Duracao).Where(d => d is not null).Select(d => d!.Value).ToList();
        TimeSpan? tempoMedio = duracoes.Count > 0
            ? TimeSpan.FromTicks((long)duracoes.Average(d => d.Ticks))
            : null;

        return new KpisDashboard(
            concluidos.Count,
            (double)noPrazo / concluidos.Count,
            (double)aprovados / concluidos.Count,
            tempoMedio);
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
        IReadOnlyDictionary<Guid, TipoAto> catalogo, int maxVolume, double maxComplexidadeMedia, bool mostrarFaixa)
    {
        var volume = protocolosDoConferente.Count;
        var noPrazo = protocolosDoConferente.Count(EstaNoPrazo);
        var aprovados = protocolosDoConferente.Count(p => p.Status == StatusProtocolo.Aprovado);
        var duracoes = protocolosDoConferente.Select(p => p.Duracao).Where(d => d is not null).Select(d => d!.Value).ToList();
        TimeSpan? tempoMedio = duracoes.Count > 0 ? TimeSpan.FromTicks((long)duracoes.Average(d => d.Ticks)) : null;
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
            conferenteId, usuario?.Nome ?? "—", conferente.Nivel, volume, tempoMedio, pctNoPrazo, pctAprovado, complexidadeMedia,
            score, faixa, new ParcelasScore(pontosVolume, pontosPrazo, pontosQualidade, pontosComplexidade));
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

        return new DesempenhoConferente(
            ConferenteId: Guid.Empty,
            Nome: null,
            Nivel: null,
            Volume: (int)Math.Round(comVolume.Average(d => d.Volume)),
            TempoMedio: tempoMedio,
            PercentualNoPrazo: comVolume.Average(d => d.PercentualNoPrazo),
            PercentualAprovado: comVolume.Average(d => d.PercentualAprovado),
            ComplexidadeMedia: comVolume.Average(d => d.ComplexidadeMedia),
            Score: (int)Math.Round(comVolume.Average(d => d.Score)),
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
    KpisDashboard Kpis,
    IReadOnlyList<DesempenhoConferente> Desempenho,
    DesempenhoConferente? MediaDaCasa,
    IReadOnlyList<DesempenhoTipoAto> PorTipoAto,
    IReadOnlyList<CumprimentoPrazoEquipe> CumprimentoPrazoEquipe);

public sealed record KpisDashboard(int AtosConferidos, double PercentualNoPrazo, double PercentualAprovado, TimeSpan? TempoMedio);

public sealed record DesempenhoConferente(
    Guid ConferenteId,
    string? Nome,
    Nivel? Nivel,
    int Volume,
    TimeSpan? TempoMedio,
    double PercentualNoPrazo,
    double PercentualAprovado,
    double ComplexidadeMedia,
    int Score,
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
