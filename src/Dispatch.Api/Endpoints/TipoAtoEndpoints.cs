using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class TipoAtoEndpoints
{
    public static void MapTipoAtoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/tipos-ato", async (ListarTiposAto casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaResponse).ToList()))
            .WithName("ListarTiposAto")
            .WithSummary("Catálogo de tipos de ato — usado pra resolver nome no alvo de uma regra de alçada (RF-31).")
            .WithTags(OpenApiTags.CentralDeRegras)
            .Produces<IReadOnlyList<TipoAtoResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/tipos-ato", async (CriarTipoAtoRequest request, CriarTipoAto casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(request.Nome, cancellationToken);
                return resultado switch
                {
                    ResultadoCriarTipoAto.Sucesso sucesso => Results.Created($"/tipos-ato/{sucesso.TipoAtoId}", new CriarTipoAtoResponse(sucesso.TipoAtoId)),
                    ResultadoCriarTipoAto.JaExiste => Results.Conflict(new { motivo = "já existe um tipo de ato com esse nome" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CriarTipoAto")
            .WithSummary("Cadastro manual — complementa o cadastro automático que a importação já faz (nome sai normalizado).")
            .WithTags(OpenApiTags.CentralDeRegras)
            .Produces<CriarTipoAtoResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        var grupo = app.MapGroup("/tipos-ato")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.CentralDeRegras);

        grupo.MapGet("/com-uso", async (ListarTiposAtoComUso casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaComUsoResponse).ToList()))
            .WithName("ListarTiposAtoComUso")
            .WithSummary("Catálogo com volume e cobertura de alçada, pra tabela da aba Tipos de ato (RF-34a).")
            .Produces<IReadOnlyList<TipoAtoComUsoResponse>>();

        grupo.MapPut("/{id:guid}", async (Guid id, RenomearTipoAtoRequest request, RenomearTipoAto casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, request.Nome, cancellationToken);
                return resultado switch
                {
                    ResultadoRenomearTipoAto.Sucesso => Results.NoContent(),
                    ResultadoRenomearTipoAto.NaoEncontrado => Results.NotFound(),
                    ResultadoRenomearTipoAto.JaExiste => Results.Conflict(new { motivo = "já existe um tipo de ato com esse nome" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("RenomearTipoAto")
            .WithSummary("RF-34b — renomear não migra protocolo/regra nenhum, os dois referenciam por Id.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPut("/{id:guid}/peso", async (Guid id, DefinirPesoRequest request, DefinirPesoDeComplexidadeDoTipoAto casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, request.Peso, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("DefinirPesoDeComplexidadeDoTipoAto")
            .WithSummary("RF-34f — alimenta o score do conferente (RF-46, Dashboard).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPut("/{id:guid}/grupo", async (Guid id, DefinirGrupoRequest request, DefinirGrupoDoTipoAto casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, request.Grupo, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("DefinirGrupoDoTipoAto")
            .WithSummary("Classificação vista na Matriz da aba Alçada (Transmissões/Sucessões/Família/Garantias/Notariais).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/ativar", async (Guid id, AtivarTipoAto casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("AtivarTipoAto")
            .WithSummary("RF-34d.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/desativar", async (Guid id, DesativarTipoAto casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("DesativarTipoAto")
            .WithSummary("RF-34d — próximos protocolos desse tipo vão para exceção; histórico não é apagado.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapDelete("/{id:guid}", async (Guid id, RemoverTipoAto casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return resultado switch
                {
                    ResultadoRemoverTipoAto.Sucesso => Results.NoContent(),
                    ResultadoRemoverTipoAto.NaoEncontrado => Results.NotFound(),
                    ResultadoRemoverTipoAto.EmUso => Results.Conflict(new { motivo = "tipo de ato em uso — protocolo ou regra de alçada referencia ele" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("RemoverTipoAto")
            .WithSummary("RF-34e — só remove se não estiver em uso (protocolo ou regra de alçada).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static TipoAtoResponse ParaResponse(TipoAto tipoAto) => new(tipoAto.Id, tipoAto.Nome, tipoAto.Ativo, tipoAto.Grupo);

    private static TipoAtoComUsoResponse ParaComUsoResponse(TipoAtoComUso tipo) =>
        new(tipo.Id, tipo.Nome, tipo.Ativo, tipo.PesoComplexidade, tipo.Grupo, tipo.Volume, tipo.ConferentesComAlcada);
}

public sealed record TipoAtoResponse(Guid Id, string Nome, bool Ativo, GrupoTipoAto? Grupo);

public sealed record CriarTipoAtoRequest(string Nome);

public sealed record CriarTipoAtoResponse(Guid TipoAtoId);

public sealed record RenomearTipoAtoRequest(string Nome);

public sealed record DefinirPesoRequest(int Peso);

public sealed record DefinirGrupoRequest(GrupoTipoAto? Grupo);

public sealed record TipoAtoComUsoResponse(
    Guid Id, string Nome, bool Ativo, int PesoComplexidade, GrupoTipoAto? Grupo, int Volume, int ConferentesComAlcada);
