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
        // Central de regras: escrever é só do Administrador (RF-30a, ADR-0039). As listagens gerais (GET /equipes e
        // /escreventes) continuam de Distribuidora e Conferente; o resto virou Administrador.
        var equipesGrupo = app.MapGroup("/equipes").WithTags(OpenApiTags.CentralDeRegras);

        equipesGrupo.MapGet("/", async (ListarEquipes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarEquipes")
            .WithSummary("Lista todas as equipes — também usado pelo filtro de equipe em Distribuição/Minha fila (RF-18e/RF-24f).")
            .Produces<IReadOnlyList<EquipeResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora), nameof(Papel.Conferente)));

        equipesGrupo.MapPost("/", async (CriarEquipeRequest request, CriarEquipe casoDeUso, CancellationToken cancellationToken) =>
            {
                if (!CorteValido(request.CortePreConferenciaHorarioCorte, request.CortePreConferenciaHorarioVencimento) ||
                    !CorteValido(request.CortePosConferenciaHorarioCorte, request.CortePosConferenciaHorarioVencimento))
                {
                    return Results.BadRequest(new { motivo = "corte de horário precisa dos dois horários (corte e vencimento), ou nenhum" });
                }

                var id = await casoDeUso.ExecutarAsync(
                    request.Nome, new Prazo(request.PrazoPreConferencia), new Prazo(request.PrazoPosConferencia),
                    request.CortePreConferenciaHorarioCorte, request.CortePreConferenciaHorarioVencimento,
                    request.CortePosConferenciaHorarioCorte, request.CortePosConferenciaHorarioVencimento,
                    cancellationToken);
                return Results.Created($"/equipes/{id}", new CriarEquipeResponse(id));
            })
            .WithName("CriarEquipe")
            .WithSummary("RF-35.")
            .Produces<CriarEquipeResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)));

        equipesGrupo.MapPut("/{id:guid}", async (
                Guid id, EditarEquipeRequest request, EditarEquipe casoDeUso, CancellationToken cancellationToken) =>
            {
                if (!CorteValido(request.CortePreConferenciaHorarioCorte, request.CortePreConferenciaHorarioVencimento) ||
                    !CorteValido(request.CortePosConferenciaHorarioCorte, request.CortePosConferenciaHorarioVencimento))
                {
                    return Results.BadRequest(new { motivo = "corte de horário precisa dos dois horários (corte e vencimento), ou nenhum" });
                }

                var encontrada = await casoDeUso.ExecutarAsync(
                    id, request.Nome, new Prazo(request.PrazoPreConferencia), new Prazo(request.PrazoPosConferencia),
                    request.CortePreConferenciaHorarioCorte, request.CortePreConferenciaHorarioVencimento,
                    request.CortePosConferenciaHorarioCorte, request.CortePosConferenciaHorarioVencimento,
                    cancellationToken);
                return encontrada ? Results.NoContent() : Results.NotFound();
            })
            .WithName("EditarEquipe")
            .WithSummary("Renomear e/ou redefinir prazo de pré e pós-conferência — recalcula vencimento dos protocolos abertos de quem está nessa equipe (RF-35/RF-36/RF-38).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)));

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
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)));

        escreventesGrupo.MapPost("/", async (CriarEscreventeRequest request, CriarEscrevente casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(request.Nome, request.EquipeId, cancellationToken);
                return resultado switch
                {
                    ResultadoCriarEscrevente.Sucesso sucesso =>
                        Results.Created($"/escreventes/{sucesso.EscreventeId}", new CriarEscreventeResponse(sucesso.EscreventeId)),
                    ResultadoCriarEscrevente.JaExiste => Results.Conflict(new { motivo = "já existe um escrevente com esse nome" }),
                    ResultadoCriarEscrevente.EquipeNaoEncontrada => Results.NotFound(new { motivo = "equipe não encontrada" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CriarEscrevente")
            .WithSummary("Cadastro manual — complementa o cadastro automático que a importação/criação de protocolo já fazem (nome sai normalizado).")
            .Produces<CriarEscreventeResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)));

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
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)));
    }

    // Corte de horário é opcional por etapa — os dois horários (corte e vencimento) precisam
    // vir juntos ou nenhum, senão a regra de Equipe.PrazoPara ficaria incompleta.
    private static bool CorteValido(TimeOnly? horarioDeCorte, TimeOnly? horarioDeVencimento) =>
        (horarioDeCorte is null) == (horarioDeVencimento is null);

    private static EquipeResponse ParaResponse(Equipe equipe) =>
        new(
            equipe.Id, equipe.Nome, equipe.PrazoPreConferencia.Tipo, equipe.PrazoPosConferencia.Tipo,
            equipe.CortePreConferenciaHorarioCorte, equipe.CortePreConferenciaHorarioVencimento,
            equipe.CortePosConferenciaHorarioCorte, equipe.CortePosConferenciaHorarioVencimento);

    private static EscreventeResponse ParaResponse(Escrevente escrevente) =>
        new(escrevente.Id, escrevente.Nome, escrevente.EquipeId);
}

// CortePreConferencia*/CortePosConferencia* são opcionais — pedido do dono ("equipe X entra na
// etapa Y depois das 16h, vence às 10h do dia seguinte"): acréscimo genérico ao TipoPrazo normal
// de cada etapa, configurável por Equipe, não hardcoded pra uma equipe específica.
public sealed record CriarEquipeRequest(
    string Nome, TipoPrazo PrazoPreConferencia, TipoPrazo PrazoPosConferencia,
    TimeOnly? CortePreConferenciaHorarioCorte = null, TimeOnly? CortePreConferenciaHorarioVencimento = null,
    TimeOnly? CortePosConferenciaHorarioCorte = null, TimeOnly? CortePosConferenciaHorarioVencimento = null);

public sealed record CriarEquipeResponse(Guid EquipeId);

public sealed record EditarEquipeRequest(
    string Nome, TipoPrazo PrazoPreConferencia, TipoPrazo PrazoPosConferencia,
    TimeOnly? CortePreConferenciaHorarioCorte, TimeOnly? CortePreConferenciaHorarioVencimento,
    TimeOnly? CortePosConferenciaHorarioCorte, TimeOnly? CortePosConferenciaHorarioVencimento);

public sealed record EquipeResponse(
    Guid Id, string Nome, TipoPrazo PrazoPreConferencia, TipoPrazo PrazoPosConferencia,
    TimeOnly? CortePreConferenciaHorarioCorte, TimeOnly? CortePreConferenciaHorarioVencimento,
    TimeOnly? CortePosConferenciaHorarioCorte, TimeOnly? CortePosConferenciaHorarioVencimento);

public sealed record MoverEscreventeRequest(Guid? EquipeId);

public sealed record EscreventeResponse(Guid Id, string Nome, Guid? EquipeId);

public sealed record CriarEscreventeRequest(string Nome, Guid? EquipeId);

public sealed record CriarEscreventeResponse(Guid EscreventeId);
