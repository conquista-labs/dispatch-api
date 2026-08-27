using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class ProtocoloEndpoints
{
    public static void MapProtocoloEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/protocolos/distribuir", async (
                DistribuirProtocoloRequest request,
                DistribuirProtocolo casoDeUso,
                IRelogio relogio,
                CancellationToken cancellationToken) =>
            {
                // Endpoint avulso, sem relatório por trás — usa "agora" como o instante do
                // andamento, já que não existe um de verdade vindo de importação nenhuma.
                var protocolo = new Protocolo(Guid.NewGuid(), request.Numero, request.TipoAtoId, request.Etapa, relogio.Agora, request.Prioridade);
                var escrevente = new Escrevente(request.EscreventeId, request.EscreventeNome, request.EquipeId);

                var resultado = await casoDeUso.ExecutarAsync(protocolo, escrevente, cancellationToken);

                return Results.Ok(ParaResponse(protocolo, resultado));
            })
            .WithName("DistribuirProtocolo")
            .WithSummary("Resolve o prazo do protocolo e decide o destino: atribuído, pool ou exceção.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces<DistribuirProtocoloResponse>()
            // Seção 3 do requisito: importação/distribuição é ação de gestão — só Distribuidora.
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
    }

    // Domain (ResultadoDistribuicao) não sai direto pro cliente HTTP — vira um DTO de
    // resposta próprio da Api, achatado, fácil de serializar e estável independente de como
    // o Domain organiza o resultado internamente.
    private static DistribuirProtocoloResponse ParaResponse(Protocolo protocolo, ResultadoDistribuicao resultado) => resultado switch
    {
        ResultadoDistribuicao.Atribuido atribuido => new DistribuirProtocoloResponse(
            protocolo.Id, "Atribuido", atribuido.Conferente.Id, Motivo: null, protocolo.VencimentoEm),

        ResultadoDistribuicao.EnviadoParaPool => new DistribuirProtocoloResponse(
            protocolo.Id, "EnviadoParaPool", ConferenteId: null, Motivo: null, protocolo.VencimentoEm),

        ResultadoDistribuicao.Excecao excecao => new DistribuirProtocoloResponse(
            protocolo.Id, "Excecao", ConferenteId: null, excecao.Motivo, protocolo.VencimentoEm),

        _ => throw new InvalidOperationException($"Resultado de distribuição não mapeado: {resultado.GetType().Name}")
    };
}

public sealed record DistribuirProtocoloRequest(
    string Numero,
    Guid TipoAtoId,
    Etapa Etapa,
    Prioridade Prioridade,
    Guid EscreventeId,
    string EscreventeNome,
    Guid? EquipeId);

public sealed record DistribuirProtocoloResponse(
    Guid ProtocoloId,
    string Resultado,
    Guid? ConferenteId,
    string? Motivo,
    DateTimeOffset? VencimentoEm);
