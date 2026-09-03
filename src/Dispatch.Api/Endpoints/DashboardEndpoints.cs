using System.Security.Claims;
using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class DashboardEndpoints
{
    public static void MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard", async (
                PeriodoDashboard periodo,
                ObterDashboard casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                // RF-45/RNF: Conferente só vê os próprios números + a média da casa, nunca a
                // lista com nome de colegas — mesmo padrão de PUT /protocolos/{id}/observacao,
                // a restrição decide por dentro conforme o papel do token, não por rota separada.
                Guid? conferenteRestritoId = null;
                if (usuario.IsInRole(nameof(Papel.Conferente)))
                {
                    var usuarioId = usuario.ObterUsuarioId();
                    var conferente = await conferentes.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);
                    if (conferente is null)
                    {
                        return Results.NotFound(new { motivo = "conferente não encontrado" });
                    }

                    conferenteRestritoId = conferente.Id;
                }

                var resultado = await casoDeUso.ExecutarAsync(periodo, conferenteRestritoId, cancellationToken);
                return Results.Ok(ParaResponse(resultado));
            })
            .WithName("ObterDashboard")
            .WithSummary("KPIs, score (40% volume + 30% prazo + 20% qualidade + 10% complexidade) e desempenho por período (RF-42 a RF-46).")
            .WithTags(OpenApiTags.Dashboard)
            .Produces<DashboardResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora), nameof(Papel.Conferente)));
    }

    private static DashboardResponse ParaResponse(ResultadoDashboard resultado) => new(
        new KpisResponse(resultado.Kpis.AtosConferidos, resultado.Kpis.PercentualNoPrazo, resultado.Kpis.PercentualAprovado, resultado.Kpis.TempoMedio),
        resultado.Desempenho.Select(ParaDesempenhoResponse).ToList(),
        resultado.MediaDaCasa is { } media ? ParaDesempenhoResponse(media) : null,
        resultado.PorTipoAto.Select(t => new DesempenhoTipoAtoResponse(t.TipoAtoId, t.Nome, t.Volume, t.TempoMedio, t.PercentualReprovacao)).ToList(),
        resultado.CumprimentoPrazoEquipe
            .Select(c => new CumprimentoPrazoEquipeResponse(c.EquipeId, c.EquipeNome, c.Etapa, c.Prazo, c.Total, c.PercentualNoPrazo))
            .ToList());

    private static DesempenhoConferenteResponse ParaDesempenhoResponse(DesempenhoConferente d) => new(
        d.ConferenteId, d.Nome, d.Nivel, d.Volume, d.TempoMedio, d.PercentualNoPrazo, d.PercentualAprovado, d.ComplexidadeMedia,
        d.Score, d.Faixa,
        d.Parcelas is { } p ? new ParcelasScoreResponse(p.Volume, p.Prazo, p.Qualidade, p.Complexidade) : null);
}

public sealed record DashboardResponse(
    KpisResponse Kpis,
    IReadOnlyList<DesempenhoConferenteResponse> Desempenho,
    DesempenhoConferenteResponse? MediaDaCasa,
    IReadOnlyList<DesempenhoTipoAtoResponse> PorTipoAto,
    IReadOnlyList<CumprimentoPrazoEquipeResponse> CumprimentoPrazoEquipe);

public sealed record KpisResponse(int AtosConferidos, double PercentualNoPrazo, double PercentualAprovado, TimeSpan? TempoMedio);

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
    double ComplexidadeMedia,
    int Score,
    FaixaBonificacao? Faixa,
    ParcelasScoreResponse? Parcelas);

public sealed record ParcelasScoreResponse(double Volume, double Prazo, double Qualidade, double Complexidade);

public sealed record DesempenhoTipoAtoResponse(Guid TipoAtoId, string Nome, int Volume, TimeSpan? TempoMedio, double PercentualReprovacao);

public sealed record CumprimentoPrazoEquipeResponse(Guid? EquipeId, string EquipeNome, Etapa Etapa, TipoPrazo? Prazo, int Total, double PercentualNoPrazo);
