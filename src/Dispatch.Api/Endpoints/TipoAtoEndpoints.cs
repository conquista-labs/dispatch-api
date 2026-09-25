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
            .WithSummary("Catálogo de tipos de ato — usado pra resolver nome no alvo de uma regra de alçada (RF-31), no filtro de tipo de ato (RF-18e/RF-24f) e no \"Peso de complexidade\" do painel de detalhe (RF-18a).")
            .WithTags(OpenApiTags.CentralDeRegras)
            .Produces<IReadOnlyList<TipoAtoResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora), nameof(Papel.Conferente)));

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
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)));

        // Central de regras: escrever é só do Administrador (RF-30a, ADR-0039). O GET raiz acima continua
        // aberto a Distribuidora e Conferente (filtros, cards, importação).
        var grupo = app.MapGroup("/tipos-ato")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)))
            .WithTags(OpenApiTags.CentralDeRegras);

        grupo.MapGet("/com-uso", async (
                ListarTiposAtoComUso casoDeUso, CancellationToken cancellationToken,
                string? busca = null, int pagina = 1, int tamanhoPagina = ListarTiposAtoComUso.TamanhoDePaginaPadrao) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(busca, pagina, tamanhoPagina, cancellationToken);
                return Results.Ok(new PaginaDeTipoAtoComUsoResponse(resultado.Itens.Select(ParaComUsoResponse).ToList(), resultado.Total));
            })
            .WithName("ListarTiposAtoComUso")
            .WithSummary("Catálogo com volume, cobertura de alçada e tempo de referência efetivo (informado → mediana de 12 meses com ≥ 30 conferências → estimativa), paginado, pra tabela da aba Tipos de ato (RF-34a, RF-46c).")
            .Produces<PaginaDeTipoAtoComUsoResponse>();

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
            {
                var resultado = await casoDeUso.ExecutarAsync(id, request.Peso, cancellationToken);
                return resultado switch
                {
                    ResultadoDefinirPesoDeComplexidade.Sucesso => Results.NoContent(),
                    ResultadoDefinirPesoDeComplexidade.NaoEncontrado => Results.NotFound(),
                    ResultadoDefinirPesoDeComplexidade.Invalido invalido => Results.BadRequest(new { motivo = invalido.Motivo }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("DefinirPesoDeComplexidadeDoTipoAto")
            .WithSummary("RF-34f — peso decimal 0,50–2,50 em passos de 0,05; alimenta o score do conferente (RF-46) e a estimativa do tempo de referência (RF-46c).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPut("/{id:guid}/tempo-referencia", async (
                Guid id, DefinirTempoDeReferenciaRequest request, DefinirTempoDeReferenciaDoTipoAto casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, request.Minutos, cancellationToken);
                return resultado switch
                {
                    ResultadoDefinirTempoDeReferencia.Sucesso => Results.NoContent(),
                    ResultadoDefinirTempoDeReferencia.NaoEncontrado => Results.NotFound(),
                    ResultadoDefinirTempoDeReferencia.Invalido invalido => Results.BadRequest(new { motivo = invalido.Motivo }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("DefinirTempoDeReferenciaDoTipoAto")
            .WithSummary("RF-34a — grava o tempo de referência informado (2–240 min); minutos nulo = \"usar histórico\" (volta à mediana, ou à estimativa sem 30 conferências).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
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

    private static TipoAtoResponse ParaResponse(TipoAto tipoAto) =>
        new(tipoAto.Id, tipoAto.Nome, tipoAto.Ativo, tipoAto.Grupo, tipoAto.PesoComplexidade);

    private static TipoAtoComUsoResponse ParaComUsoResponse(TipoAtoComUso tipo) =>
        new(tipo.Id, tipo.Nome, tipo.Ativo, tipo.PesoComplexidade, tipo.Grupo, tipo.Volume, tipo.ConferentesComAlcada,
            new TempoReferenciaResponse(
                tipo.TempoReferencia.Minutos, tipo.TempoReferencia.Origem, tipo.TempoReferencia.InformadoMinutos,
                tipo.TempoReferencia.MedianaMinutos, tipo.TempoReferencia.ConferenciasNoHistorico));
}

// PesoComplexidade (RF-18a, fatia 5 do Dashboard v2): decimal 0,50–2,50.
public sealed record TipoAtoResponse(Guid Id, string Nome, bool Ativo, GrupoTipoAto? Grupo, decimal PesoComplexidade);

public sealed record CriarTipoAtoRequest(string Nome);

public sealed record CriarTipoAtoResponse(Guid TipoAtoId);

public sealed record RenomearTipoAtoRequest(string Nome);

// Decimal 0,50–2,50, múltiplo de 0,05 (era inteiro até a fatia 5 do Dashboard v2).
public sealed record DefinirPesoRequest(decimal Peso);

// Nulo = "usar histórico" (apaga o informado).
public sealed record DefinirTempoDeReferenciaRequest(int? Minutos);

public sealed record DefinirGrupoRequest(GrupoTipoAto? Grupo);

public sealed record TipoAtoComUsoResponse(
    Guid Id, string Nome, bool Ativo, decimal PesoComplexidade, GrupoTipoAto? Grupo, int Volume, int ConferentesComAlcada,
    TempoReferenciaResponse TempoReferencia);

// RF-34a/RF-46c. Minutos = referência efetiva; Origem = Informado | Historico | Estimado. MedianaMinutos
// vem mesmo com valor informado (null = menos de 30 conferências válidas em 12 meses — o "usar
// histórico" não tem pra onde voltar). ConferenciasNoHistorico = conferências válidas na janela (o N de
// "mediana de N atos"), depois de descartar as > 4× a estimativa.
public sealed record TempoReferenciaResponse(
    int Minutos, OrigemTempoReferencia Origem, int? InformadoMinutos, int? MedianaMinutos, int ConferenciasNoHistorico);

public sealed record PaginaDeTipoAtoComUsoResponse(IReadOnlyList<TipoAtoComUsoResponse> Itens, int Total);
