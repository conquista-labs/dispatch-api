using System.Security.Claims;
using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        // Grupo só pra as duas leituras dividirem a mesma regra de papel — cada rota continua
        // decidindo a visão (gestão ou restrita) por dentro, pelo papel do token.
        var grupo = app.MapGroup("/dashboard")
            .WithTags(OpenApiTags.Dashboard)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora), nameof(Papel.Conferente)));

        grupo.MapGet("", async (
                PeriodoDashboard periodo,
                ObterDashboard casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var restricao = await ResolverVisaoRestritaAsync(usuario, conferentes, cancellationToken);
                if (restricao.ConferenteNaoEncontrado)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var resultado = await casoDeUso.ExecutarAsync(
                    periodo, restricao.ConferenteId, incluirAvaliacaoDePessoal: usuario.EhAdministrador(), cancellationToken);
                return Results.Ok(ParaResponse(resultado));
            })
            .WithName("ObterDashboard")
            .WithSummary("KPIs (com o mesmo trecho do período anterior), série por dia útil/semana, score (40% volume + 30% prazo + 20% qualidade + 10% complexidade) e desempenho por período de calendário no dia de Brasília (RF-42 a RF-46).")
            .Produces<DashboardResponse>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapGet("/hoje", async (
                ObterPainelDeHoje casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var restricao = await ResolverVisaoRestritaAsync(usuario, conferentes, cancellationToken);
                if (restricao.ConferenteNaoEncontrado)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var painel = await casoDeUso.ExecutarAsync(restricao.ConferenteId, cancellationToken);
                return Results.Ok(ParaResponse(painel));
            })
            .WithName("ObterPainelDeHoje")
            .WithSummary("RF-42a — \"Hoje, agora\" (gestão: conferidos hoje, fila, em risco, exceções, gargalo por equipe) ou \"Seu dia\" (conferente: conferidos hoje, na mão, em risco com ele). Hoje = dia de Brasília.")
            .Produces<PainelDeHojeResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    // RF-45/RNF: Conferente só vê os próprios números, nunca os de colegas — a restrição decide
    // por dentro conforme o papel do token, não por rota separada (mesmo padrão de
    // PUT /protocolos/{id}/observacao). "&& !Distribuidora": alguém com os dois papéis
    // (distribuidora que também confere) vê a visão de gestão completa sempre — o papel Conferente
    // aqui só soma a capacidade de conferir, nunca reduz o que ela já vê como distribuidora
    // (decisão confirmada com o dono). O Administrador carrega a claim Distribuidora (ADR-0039).
    private static async Task<VisaoRestrita> ResolverVisaoRestritaAsync(
        ClaimsPrincipal usuario, IConferenteRepository conferentes, CancellationToken cancellationToken)
    {
        if (!usuario.IsInRole(nameof(Papel.Conferente)) || usuario.IsInRole(nameof(Papel.Distribuidora)))
        {
            return new VisaoRestrita(ConferenteId: null, ConferenteNaoEncontrado: false);
        }

        var conferente = await conferentes.ObterPorUsuarioIdAsync(usuario.ObterUsuarioId(), cancellationToken);
        return conferente is null
            ? new VisaoRestrita(ConferenteId: null, ConferenteNaoEncontrado: true)
            : new VisaoRestrita(conferente.Id, ConferenteNaoEncontrado: false);
    }

    private sealed record VisaoRestrita(Guid? ConferenteId, bool ConferenteNaoEncontrado);

    private static PainelDeHojeResponse ParaResponse(PainelDeHoje painel) => new(
        painel.Visao,
        painel.AtualizadoEm,
        painel.ConferidosHoje,
        painel.NaFila is { } naFila ? new NaFilaHojeResponse(naFila.Pool, naFila.ComConferente) : null,
        painel.NaMao is { } naMao ? new NaMaoHojeResponse(naMao.Total, naMao.EmConferencia) : null,
        new EmRiscoHojeResponse(painel.EmRisco.Estourados, painel.EmRisco.VencemEmUmaHora),
        painel.Excecoes,
        painel.Gargalo is { } gargalo ? new GargaloHojeResponse(gargalo.EquipeId, gargalo.Quantidade) : null);

    private static DashboardResponse ParaResponse(ResultadoDashboard resultado) => new(
        resultado.PeriodoInicio,
        resultado.PeriodoFim,
        ParaKpisResponse(resultado.Kpis),
        ParaKpisResponse(resultado.KpisAnterior),
        new SerieResponse(
            resultado.Serie.Granularidade,
            resultado.Serie.Pontos.Select(p => new PontoSerieResponse(p.Inicio, p.Conferidos, p.Estourados, p.Futuro)).ToList()),
        resultado.Desempenho.Select(ParaDesempenhoResponse).ToList(),
        resultado.MediaDaCasa is { } media ? ParaDesempenhoResponse(media) : null,
        resultado.PorTipoAto.Select(t => new DesempenhoTipoAtoResponse(t.TipoAtoId, t.Nome, t.Volume, t.TempoMedio, t.PercentualReprovacao)).ToList(),
        resultado.CumprimentoPrazoEquipe
            .Select(c => new CumprimentoPrazoEquipeResponse(c.EquipeId, c.EquipeNome, c.Etapa, c.Prazo, c.Total, c.PercentualNoPrazo))
            .ToList());

    private static KpisResponse ParaKpisResponse(KpisDashboard k) =>
        new(k.AtosConferidos, k.PercentualNoPrazo, k.PercentualAprovado, k.PercentualAprovadoNaPrimeira, k.TempoMedio);

    private static DesempenhoConferenteResponse ParaDesempenhoResponse(DesempenhoConferente d) => new(
        d.ConferenteId, d.Nome, d.Nivel, d.Volume, d.TempoMedio, d.PercentualNoPrazo, d.PercentualAprovado,
        d.PercentualAprovadoNaPrimeira, d.ComplexidadeMedia, d.Score, d.Faixa,
        d.Parcelas is { } p ? new ParcelasScoreResponse(p.Volume, p.Prazo, p.Qualidade, p.Complexidade) : null);
}

// PeriodoInicio/PeriodoFim: o intervalo de calendário usado (instantes UTC; fim = agora). KpisAnterior:
// mesma conta sobre o mesmo trecho do período anterior (RF-42b) — também na visão restrita, com os
// números do próprio conferente. Serie: RF-42c (ver SerieDoPeriodo).
public sealed record DashboardResponse(
    DateTimeOffset PeriodoInicio,
    DateTimeOffset PeriodoFim,
    KpisResponse Kpis,
    KpisResponse KpisAnterior,
    SerieResponse Serie,
    IReadOnlyList<DesempenhoConferenteResponse> Desempenho,
    DesempenhoConferenteResponse? MediaDaCasa,
    IReadOnlyList<DesempenhoTipoAtoResponse> PorTipoAto,
    IReadOnlyList<CumprimentoPrazoEquipeResponse> CumprimentoPrazoEquipe);

// PercentualAprovadoNaPrimeira: 0–1, nulo sem nenhuma 1ª conferência (RF-24k) no recorte (RF-43).
public sealed record KpisResponse(
    int AtosConferidos, double PercentualNoPrazo, double PercentualAprovado, double? PercentualAprovadoNaPrimeira, TimeSpan? TempoMedio);

// Granularidade Dia (Semana/Mes: dias úteis do período inteiro + fim de semana com conferência) ou
// Semana (Trimestre: uma por segunda-feira). Inicio = dia local de Brasília ("2026-09-01").
public sealed record SerieResponse(GranularidadeSerie Granularidade, IReadOnlyList<PontoSerieResponse> Pontos);

public sealed record PontoSerieResponse(DateOnly Inicio, int Conferidos, int Estourados, bool Futuro);

// Nome/Nivel/Parcelas nulos quando a linha é "MediaDaCasa" (RF-45 — sem identificar ninguém,
// sem detalhar parcela de ninguém). Faixa é null nesses dois casos E também na visão restrita
// do próprio conferente — RF-45: "o próprio score com o detalhamento das parcelas... sem
// faixa de bônus" (o conferente vê as 4 parcelas, mas não a faixa de bonificação).
public sealed record DesempenhoConferenteResponse(
    Guid ConferenteId,
    string? Nome,
    Nivel? Nivel,
    int Volume,
    TimeSpan? TempoMedio,
    double PercentualNoPrazo,
    double PercentualAprovado,
    double? PercentualAprovadoNaPrimeira,
    double ComplexidadeMedia,
    // Nulo pra quem não é Administrador — nível, score, faixa e parcelas (ADR-0039).
    int? Score,
    FaixaBonificacao? Faixa,
    ParcelasScoreResponse? Parcelas);

public sealed record ParcelasScoreResponse(double Volume, double Prazo, double Qualidade, double Complexidade);

public sealed record DesempenhoTipoAtoResponse(Guid TipoAtoId, string Nome, int Volume, TimeSpan? TempoMedio, double PercentualReprovacao);

public sealed record CumprimentoPrazoEquipeResponse(Guid? EquipeId, string EquipeNome, Etapa Etapa, TipoPrazo? Prazo, int Total, double PercentualNoPrazo);

// RF-42a. `Visao` diz qual das duas formas veio: Gestao traz NaFila/Excecoes/Gargalo e NaMao nulo;
// Conferente traz NaMao e os três de gestão nulos. Gargalo nulo = nenhuma equipe concentra mais de
// um protocolo em risco; EquipeId nulo dentro dele = o grupo "sem equipe". O nome da equipe o front
// resolve (GET /equipes).
public sealed record PainelDeHojeResponse(
    VisaoPainelHoje Visao,
    DateTimeOffset AtualizadoEm,
    int ConferidosHoje,
    NaFilaHojeResponse? NaFila,
    NaMaoHojeResponse? NaMao,
    EmRiscoHojeResponse EmRisco,
    int? Excecoes,
    GargaloHojeResponse? Gargalo);

public sealed record NaFilaHojeResponse(int Pool, int ComConferente);

public sealed record NaMaoHojeResponse(int Total, int EmConferencia);

public sealed record EmRiscoHojeResponse(int Estourados, int VencemEmUmaHora);

public sealed record GargaloHojeResponse(Guid? EquipeId, int Quantidade);
