using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class EquipeEndpoints
{
    public static void MapEquipeEndpoints(this IEndpointRouteBuilder app)
    {
        var equipesGrupo = app.MapGroup("/equipes")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.CentralDeRegras);

        equipesGrupo.MapGet("/", async (ListarEquipes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarEquipes")
            .WithSummary("Lista todas as equipes.")
            .Produces<IReadOnlyList<EquipeResponse>>();

        equipesGrupo.MapPost("/", async (CriarEquipeRequest request, CriarEquipe casoDeUso, CancellationToken cancellationToken) =>
            {
                var id = await casoDeUso.ExecutarAsync(
                    request.Nome, new Prazo(request.PrazoPreConferencia), new Prazo(request.PrazoPosConferencia), cancellationToken);
                return Results.Created($"/equipes/{id}", new CriarEquipeResponse(id));
            })
            .WithName("CriarEquipe")
            .WithSummary("RF-35.")
            .Produces<CriarEquipeResponse>(StatusCodes.Status201Created);

        equipesGrupo.MapPut("/{id:guid}", async (
                Guid id, EditarEquipeRequest request, EditarEquipe casoDeUso, CancellationToken cancellationToken) =>
            {
                var encontrada = await casoDeUso.ExecutarAsync(
                    id, request.Nome, new Prazo(request.PrazoPreConferencia), new Prazo(request.PrazoPosConferencia), cancellationToken);
                return encontrada ? Results.NoContent() : Results.NotFound();
            })
            .WithName("EditarEquipe")
            .WithSummary("Renomear e/ou redefinir prazo de pré e pós-conferência (RF-35/RF-36). Não recalcula vencimentos abertos (RF-38 pendente).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        var escreventesGrupo = app.MapGroup("/escreventes")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.CentralDeRegras);

        escreventesGrupo.MapGet("/sem-equipe", async (ListarEscreventesSemEquipe casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarEscreventesSemEquipe")
            .WithSummary("RF-37.")
            .Produces<IReadOnlyList<EscreventeResponse>>();

        escreventesGrupo.MapPost("/{id:guid}/mover", async (
                Guid id, MoverEscreventeRequest request, MoverEscreventeParaEquipe casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, request.EquipeId, cancellationToken);
                return resultado switch
                {
                    ResultadoMoverEscrevente.Sucesso => Results.NoContent(),
                    ResultadoMoverEscrevente.EscreventeNaoEncontrado => Results.NotFound(new { motivo = "escrevente não encontrado" }),
                    ResultadoMoverEscrevente.EquipeNaoEncontrada => Results.NotFound(new { motivo = "equipe não encontrada" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("MoverEscreventeParaEquipe")
            .WithSummary("Move o escrevente pra outra equipe, ou tira dele (equipeId nulo) — RF-35/RF-37.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static EquipeResponse ParaResponse(Equipe equipe) =>
        new(equipe.Id, equipe.Nome, equipe.PrazoPreConferencia.Tipo, equipe.PrazoPosConferencia.Tipo);

    private static EscreventeResponse ParaResponse(Escrevente escrevente) =>
        new(escrevente.Id, escrevente.Nome, escrevente.EquipeId);
}

public sealed record CriarEquipeRequest(string Nome, TipoPrazo PrazoPreConferencia, TipoPrazo PrazoPosConferencia);

public sealed record CriarEquipeResponse(Guid EquipeId);

public sealed record EditarEquipeRequest(string Nome, TipoPrazo PrazoPreConferencia, TipoPrazo PrazoPosConferencia);

public sealed record EquipeResponse(Guid Id, string Nome, TipoPrazo PrazoPreConferencia, TipoPrazo PrazoPosConferencia);

public sealed record MoverEscreventeRequest(Guid? EquipeId);

public sealed record EscreventeResponse(Guid Id, string Nome, Guid? EquipeId);
