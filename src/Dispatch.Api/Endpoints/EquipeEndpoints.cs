using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class EquipeEndpoints
{
    public static void MapEquipeEndpoints(this IEndpointRouteBuilder app)
    {
        // Sem policy no grupo em si — cada rota declara a sua. As duas leituras (`GET /`) são
        // usadas pelo filtro de equipe/escrevente de Distribuição **e** Minha fila (RF-18e/
        // RF-24f), então qualquer Conferente também precisa; as mutações (criar/editar/mover)
        // continuam exclusivas da Distribuidora (RF-35 a RF-37, ação de gestão). Repetir
        // `.RequireAuthorization(...)` numa rota individual **não substitui** a policy do
        // grupo — as duas se combinam com E, não OU (cada `[Authorize]`/`RequireAuthorization`
        // aplicado é mais um requisito que TODOS precisam satisfazer) — por isso o grupo não
        // pode ter uma policy só de Distribuidora se alguma rota dele precisa ser mais aberta.
        var equipesGrupo = app.MapGroup("/equipes").WithTags(OpenApiTags.CentralDeRegras);

        equipesGrupo.MapGet("/", async (ListarEquipes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarEquipes")
            .WithSummary("Lista todas as equipes — também usado pelo filtro de equipe em Distribuição/Minha fila (RF-18e/RF-24f).")
            .Produces<IReadOnlyList<EquipeResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora), nameof(Papel.Conferente)));

        equipesGrupo.MapPost("/", async (CriarEquipeRequest request, CriarEquipe casoDeUso, CancellationToken cancellationToken) =>
            {
                var id = await casoDeUso.ExecutarAsync(
                    request.Nome, new Prazo(request.PrazoPreConferencia), new Prazo(request.PrazoPosConferencia), cancellationToken);
                return Results.Created($"/equipes/{id}", new CriarEquipeResponse(id));
            })
            .WithName("CriarEquipe")
            .WithSummary("RF-35.")
            .Produces<CriarEquipeResponse>(StatusCodes.Status201Created)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        equipesGrupo.MapPut("/{id:guid}", async (
                Guid id, EditarEquipeRequest request, EditarEquipe casoDeUso, CancellationToken cancellationToken) =>
            {
                var encontrada = await casoDeUso.ExecutarAsync(
                    id, request.Nome, new Prazo(request.PrazoPreConferencia), new Prazo(request.PrazoPosConferencia), cancellationToken);
                return encontrada ? Results.NoContent() : Results.NotFound();
            })
            .WithName("EditarEquipe")
            .WithSummary("Renomear e/ou redefinir prazo de pré e pós-conferência — recalcula vencimento dos protocolos abertos de quem está nessa equipe (RF-35/RF-36/RF-38).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        var escreventesGrupo = app.MapGroup("/escreventes").WithTags(OpenApiTags.CentralDeRegras);

        escreventesGrupo.MapGet("/", async (ListarEscreventes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarEscreventes")
            .WithSummary("Lista todos os escreventes — usado pra resolver nome/equipe dos cards da visão de distribuição (RF-14) e do filtro de equipe (RF-18e/RF-24f).")
            .Produces<IReadOnlyList<EscreventeResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora), nameof(Papel.Conferente)));

        escreventesGrupo.MapGet("/sem-equipe", async (ListarEscreventesSemEquipe casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarEscreventesSemEquipe")
            .WithSummary("RF-37.")
            .Produces<IReadOnlyList<EscreventeResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

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
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
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
