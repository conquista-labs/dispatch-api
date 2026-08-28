using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class ConferenteEndpoints
{
    public static void MapConferenteEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/conferentes")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.Conferentes);

        grupo.MapGet("/", async (ListarConferentes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok(await casoDeUso.ExecutarAsync(cancellationToken)))
            .WithName("ListarConferentes")
            .WithSummary("Lista todos os conferentes com nome/e-mail — front usa pra resolver identidade em qualquer tela que só tem conferenteId (RF-25).")
            .Produces<IReadOnlyList<ConferenteComUsuario>>();

        grupo.MapPost("/", async (
                CadastrarConferenteRequest request,
                CadastrarConferente casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(
                    request.Nome, request.Email, request.Senha, request.Nivel, request.JornadaHoras, cancellationToken);

                return resultado switch
                {
                    ResultadoCadastroConferente.Sucesso sucesso =>
                        Results.Created($"/conferentes/{sucesso.ConferenteId}", new CadastrarConferenteResponse(sucesso.ConferenteId)),
                    ResultadoCadastroConferente.EmailJaCadastrado =>
                        Results.Conflict(new { motivo = "e-mail já cadastrado" }),
                    _ => throw new InvalidOperationException($"Resultado de cadastro não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CadastrarConferente")
            .WithSummary("Cadastra um conferente (RF-25) — cria também o usuário de login (papel Conferente).")
            .Produces<CadastrarConferenteResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPut("/{id:guid}/nivel-jornada", async (
                Guid id,
                EditarNivelEJornadaRequest request,
                EditarNivelEJornada casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var encontrado = await casoDeUso.ExecutarAsync(id, request.Nivel, request.JornadaHoras, cancellationToken);
                return encontrado ? Results.NoContent() : Results.NotFound();
            })
            .WithName("EditarNivelEJornadaConferente")
            .WithSummary("Edita nível e jornada de um conferente (RF-25/RF-26).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/presenca", async (
                Guid id,
                MarcarPresencaRequest request,
                MarcarPresenca casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var encontrado = await casoDeUso.ExecutarAsync(id, request.Presente, cancellationToken);
                return encontrado ? Results.NoContent() : Results.NotFound();
            })
            .WithName("MarcarPresencaConferente")
            .WithSummary("Marca presença/ausência de um conferente na escala (RF-27).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", async (
                Guid id,
                RemoverConferente casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var encontrado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return encontrado ? Results.NoContent() : Results.NotFound();
            })
            .WithName("RemoverConferente")
            .WithSummary("Remove um conferente (RF-25) — desativa o usuário e tira da escala.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapGet("/cobertura", async (ObterCoberturaDeAlcada casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok(await casoDeUso.ExecutarAsync(cancellationToken)))
            .WithName("ObterCoberturaDeAlcada")
            .WithSummary("Tipos de ato em circulação sem ninguém habilitado, ou dependentes de uma só pessoa (RF-30).")
            .Produces<CoberturaAlcada>();
    }
}

public sealed record CadastrarConferenteRequest(string Nome, string Email, string Senha, Nivel Nivel, double JornadaHoras);

public sealed record CadastrarConferenteResponse(Guid ConferenteId);

public sealed record EditarNivelEJornadaRequest(Nivel Nivel, double JornadaHoras);

public sealed record MarcarPresencaRequest(bool Presente);
