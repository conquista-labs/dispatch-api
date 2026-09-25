using Dispatch.Domain;

namespace Dispatch.Application;

// RF-42a ("Hoje, agora" na gestão / "Seu dia" no conferente): a foto do dia, não do período —
// por isso é um caso de uso separado de ObterDashboard (que olha o período de calendário só sobre
// concluídos) e uma rota própria (o front recarrega esta faixa com frequência maior que o resto).
// "Hoje" é o dia de Brasília (FusoHorario), decidido aqui via IRelogio — a porta só sabe filtrar
// "desde" um instante, igual a ObterConcluidosHoje e ObterVisaoDistribuicao.
public sealed class ObterPainelDeHoje(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IRelogio relogio)
{
    // "Vence em 1h" (RF-42a). Fixo no requisito, diferente das faixas do semáforo, que são
    // configuração — mas a classificação em si reaproveita Semaforo.Calcular, pra "estourado"
    // aqui ser exatamente o mesmo Vermelho que o card mostra na Distribuição.
    public static readonly TimeSpan JanelaDeRisco = TimeSpan.FromHours(1);

    // `conferenteRestritoId` nulo = visão de gestão (mesmo parâmetro de ObterDashboard: o endpoint
    // decide pelo papel do token, o caso de uso só recebe "de quem" é a visão restrita).
    public async Task<PainelDeHoje> ExecutarAsync(Guid? conferenteRestritoId, CancellationToken cancellationToken = default)
    {
        var agora = relogio.Agora;
        var inicioDoDia = FusoHorario.InicioDoDiaLocal(agora);

        return conferenteRestritoId is { } conferenteId
            ? await SeuDiaAsync(conferenteId, agora, inicioDoDia, cancellationToken)
            : await HojeDaGestaoAsync(agora, inicioDoDia, cancellationToken);
    }

    private async Task<PainelDeHoje> HojeDaGestaoAsync(DateTimeOffset agora, DateTimeOffset inicioDoDia, CancellationToken cancellationToken)
    {
        // Uma query só: a mesma da visão de Distribuição, com a janela dos concluídos no início do
        // dia local — traz todo o trabalho em andamento (Pool/Atribuído/Conferindo/Exceção, sem
        // corte de data) + os concluídos de hoje; Descartado e Excluído já saem no banco.
        var doDia = await protocolos.ObterParaVisaoDistribuicaoAsync(loteImportacaoId: null, inicioDoDia, cancellationToken);

        var conferidosHoje = doDia.Count(p => ConcluidoDesde(p, inicioDoDia));
        var abertos = doDia.Where(p => p.Status is StatusProtocolo.Pool or StatusProtocolo.Atribuido
            or StatusProtocolo.Conferindo or StatusProtocolo.Excecao).ToList();

        var naFila = new NaFilaHoje(
            Pool: abertos.Count(p => p.Status == StatusProtocolo.Pool),
            ComConferente: abertos.Count(p => p.Status is StatusProtocolo.Atribuido or StatusProtocolo.Conferindo));
        var excecoes = abertos.Count(p => p.Status == StatusProtocolo.Excecao);

        var emRiscoPorFaixa = abertos
            .Select(p => (Protocolo: p, Faixa: FaixaDeRisco(p, agora)))
            .Where(x => x.Faixa is FaixaSemaforo.Vermelho or FaixaSemaforo.Laranja)
            .ToList();
        var emRisco = new EmRiscoHoje(
            Estourados: emRiscoPorFaixa.Count(x => x.Faixa == FaixaSemaforo.Vermelho),
            VencemEmUmaHora: emRiscoPorFaixa.Count(x => x.Faixa == FaixaSemaforo.Laranja));

        // Equipe vem do escrevente (é o que o protocolo guarda); uma leitura da tabela inteira de
        // escreventes (pequena) em vez de um lookup por protocolo — sem N+1.
        var gargalo = emRiscoPorFaixa.Count > 1
            ? CalcularGargalo(emRiscoPorFaixa.Select(x => x.Protocolo).ToList(), await EquipePorEscreventeAsync(cancellationToken))
            : null;

        return new PainelDeHoje(
            VisaoPainelHoje.Gestao, agora, conferidosHoje, naFila, NaMao: null, emRisco, excecoes, gargalo);
    }

    private async Task<PainelDeHoje> SeuDiaAsync(
        Guid conferenteId, DateTimeOffset agora, DateTimeOffset inicioDoDia, CancellationToken cancellationToken)
    {
        // "Na sua mão" = Atribuído + Conferindo do próprio conferente (pausado continua Conferindo).
        // Três consultas pequenas e filtradas por dono no banco — as mesmas da Minha fila.
        var atribuidos = await protocolos.ObterAtribuidosAAsync(conferenteId, cancellationToken);
        var emConferencia = await protocolos.ObterEmConferenciaPorConferenteAsync(conferenteId, cancellationToken);
        var concluidosHoje = await protocolos.ObterConcluidosPorConferenteAsync(conferenteId, inicioDoDia, cancellationToken);

        var naMao = atribuidos.Concat(emConferencia).ToList();
        var faixas = naMao.Select(p => FaixaDeRisco(p, agora)).ToList();

        return new PainelDeHoje(
            VisaoPainelHoje.Conferente,
            agora,
            ConferidosHoje: concluidosHoje.Count(p => ConcluidoDesde(p, inicioDoDia)),
            NaFila: null,
            new NaMaoHoje(Total: naMao.Count, EmConferencia: emConferencia.Count),
            new EmRiscoHoje(
                Estourados: faixas.Count(f => f == FaixaSemaforo.Vermelho),
                VencemEmUmaHora: faixas.Count(f => f == FaixaSemaforo.Laranja)),
            Excecoes: null,
            Gargalo: null);
    }

    private static bool ConcluidoDesde(Protocolo protocolo, DateTimeOffset inicioDoDia) =>
        protocolo.Status is StatusProtocolo.Aprovado or StatusProtocolo.Reprovado && protocolo.ConcluidoEm >= inicioDoDia;

    // Vermelho = vencimento < agora (estourado); Laranja = agora ≤ vencimento < agora + 1h.
    // Sem vencimento gravado não há risco a medir — fica fora das duas contagens.
    private static FaixaSemaforo? FaixaDeRisco(Protocolo protocolo, DateTimeOffset agora) =>
        protocolo.VencimentoEm is { } vencimento
            ? Semaforo.Calcular(vencimento, agora, faixaAtencao: JanelaDeRisco, faixaUrgente: JanelaDeRisco)
            : null;

    private async Task<IReadOnlyDictionary<Guid, Guid?>> EquipePorEscreventeAsync(CancellationToken cancellationToken) =>
        (await escreventes.ObterTodosAsync(cancellationToken)).ToDictionary(e => e.Id, e => e.EquipeId);

    // RF-42a: "quando uma equipe concentra mais de um protocolo estourado ou vencendo em 1h".
    // Sem equipe (escrevente sem equipe, ou que nem existe mais) é um grupo próprio (EquipeId
    // nulo), como no cumprimento de prazo por equipe do Dashboard. Empate: menor EquipeId entre
    // as equipes de verdade; o grupo "sem equipe" só vence se for o único no topo — é o menos
    // acionável dos dois, e um id nulo não é "o menor id". O nome da equipe o front resolve.
    private static GargaloHoje? CalcularGargalo(IReadOnlyCollection<Protocolo> emRisco, IReadOnlyDictionary<Guid, Guid?> equipePorEscrevente)
    {
        var maior = emRisco
            .GroupBy(p => equipePorEscrevente.GetValueOrDefault(p.EscreventeId))
            .Select(g => new GargaloHoje(g.Key, g.Count()))
            .OrderByDescending(g => g.Quantidade)
            .ThenBy(g => g.EquipeId is null)
            .ThenBy(g => g.EquipeId)
            .First();

        return maior.Quantidade > 1 ? maior : null;
    }
}

public enum VisaoPainelHoje
{
    Gestao,
    Conferente
}

// Campos só de gestão (NaFila, Excecoes, Gargalo) são nulos na visão do conferente, e NaMao é
// nulo na de gestão — um tipo só, com a visão explícita, pra o front ramificar por `Visao`.
public sealed record PainelDeHoje(
    VisaoPainelHoje Visao,
    DateTimeOffset AtualizadoEm,
    int ConferidosHoje,
    NaFilaHoje? NaFila,
    NaMaoHoje? NaMao,
    EmRiscoHoje EmRisco,
    int? Excecoes,
    GargaloHoje? Gargalo);

public sealed record NaFilaHoje(int Pool, int ComConferente);

public sealed record NaMaoHoje(int Total, int EmConferencia);

public sealed record EmRiscoHoje(int Estourados, int VencemEmUmaHora);

public sealed record GargaloHoje(Guid? EquipeId, int Quantidade);
