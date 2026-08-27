using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class RegraAlcadaEndpoints
{
    public static void MapRegraAlcadaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/regras-alcada")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.CentralDeRegras);

        grupo.MapGet("/", async (ListarRegrasAlcada casoDeUso, CancellationToken cancellationToken) =>
            {
                var todas = await casoDeUso.ExecutarAsync(cancellationToken);
                return Results.Ok(todas.Select(ParaResponse).ToList());
            })
            .WithName("ListarRegrasAlcada")
            .WithSummary("Lista todas as regras de alçada, ativas e inativas.")
            .Produces<IReadOnlyList<RegraAlcadaResponse>>();

        grupo.MapPost("/", async (CriarRegraAlcadaRequest request, CriarRegraAlcada casoDeUso, CancellationToken cancellationToken) =>
            {
                SujeitoAlcada sujeito;
                if (request.SujeitoNivel is { } nivel && request.SujeitoConferenteId is null)
                {
                    sujeito = new SujeitoAlcada.PorNivel(nivel);
                }
                else if (request.SujeitoConferenteId is { } conferenteId && request.SujeitoNivel is null)
                {
                    sujeito = new SujeitoAlcada.PorPessoa(conferenteId);
                }
                else
                {
                    return Results.BadRequest(new { motivo = "informe exatamente um entre sujeitoNivel e sujeitoConferenteId" });
                }

                AlvoAlcada alvo;
                if (request.AlvoEtapa is { } etapa && request.AlvoTipoAtoId is null)
                {
                    alvo = new AlvoAlcada.PorEtapa(etapa);
                }
                else if (request.AlvoTipoAtoId is { } tipoAtoId && request.AlvoEtapa is null)
                {
                    alvo = new AlvoAlcada.PorTipoAto(tipoAtoId);
                }
                else
                {
                    return Results.BadRequest(new { motivo = "informe exatamente um entre alvoEtapa e alvoTipoAtoId" });
                }

                var resultado = await casoDeUso.ExecutarAsync(sujeito, request.Permissao, alvo, cancellationToken);
                return resultado switch
                {
                    ResultadoCriarRegraAlcada.Sucesso sucesso =>
                        Results.Created($"/regras-alcada/{sucesso.RegraId}", new CriarRegraAlcadaResponse(sucesso.RegraId)),
                    ResultadoCriarRegraAlcada.ConferenteNaoEncontrado =>
                        Results.NotFound(new { motivo = "conferente não encontrado" }),
                    ResultadoCriarRegraAlcada.TipoAtoNaoEncontrado =>
                        Results.NotFound(new { motivo = "tipo de ato não encontrado" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CriarRegraAlcada")
            .WithSummary("Cria uma regra de alçada — quem (nível ou pessoa) pode/não pode conferir tal tipo/etapa (RF-31).")
            .Produces<CriarRegraAlcadaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/ativar", async (Guid id, AtivarRegraAlcada casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("AtivarRegraAlcada")
            .WithSummary("RF-33.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/desativar", async (Guid id, DesativarRegraAlcada casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("DesativarRegraAlcada")
            .WithSummary("RF-33.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", async (Guid id, RemoverRegraAlcada casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("RemoverRegraAlcada")
            .WithSummary("RF-33.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        app.MapGet("/conferentes/alcance", async (ObterAlcancePorConferente casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok(await casoDeUso.ExecutarAsync(cancellationToken)))
            .WithName("ObterAlcancePorConferente")
            .WithSummary("Painel de alcance de cada conferente — quantos tipos e quais etapas ele alcança hoje (RF-34).")
            .WithTags(OpenApiTags.CentralDeRegras)
            .Produces<IReadOnlyList<AlcanceDoConferente>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
    }

    private static RegraAlcadaResponse ParaResponse(RegraAlcada regra) => new(
        regra.Id,
        (regra.Sujeito as SujeitoAlcada.PorNivel)?.Nivel,
        (regra.Sujeito as SujeitoAlcada.PorPessoa)?.ConferenteId,
        regra.Permissao,
        (regra.Alvo as AlvoAlcada.PorEtapa)?.Etapa,
        (regra.Alvo as AlvoAlcada.PorTipoAto)?.TipoAtoId,
        regra.Origem,
        regra.Ativa);
}

public sealed record CriarRegraAlcadaRequest(
    Nivel? SujeitoNivel,
    Guid? SujeitoConferenteId,
    PermissaoRegra Permissao,
    Etapa? AlvoEtapa,
    Guid? AlvoTipoAtoId);

public sealed record CriarRegraAlcadaResponse(Guid RegraId);

public sealed record RegraAlcadaResponse(
    Guid Id,
    Nivel? SujeitoNivel,
    Guid? SujeitoConferenteId,
    PermissaoRegra Permissao,
    Etapa? AlvoEtapa,
    Guid? AlvoTipoAtoId,
    OrigemRegra Origem,
    bool Ativa);
