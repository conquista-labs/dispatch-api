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
                ITipoAtoRepository tiposAto,
                IEscreventeRepository escreventes,
                IRelogio relogio,
                CancellationToken cancellationToken) =>
            {
                // Mesmo cuidado do ImportarLote: não confia cegamente que o TipoAtoId recebido
                // existe — se não existir no catálogo, vira nulo (tipo desconhecido, RF-09),
                // em vez de quebrar a FK na hora de gravar.
                var tipoConhecido = (await tiposAto.ObterTodosAsync(cancellationToken)).Any(t => t.Id == request.TipoAtoId);
                var tipoAtoId = tipoConhecido ? request.TipoAtoId : (Guid?)null;

                // Mesma resolução por nome do ImportarLote — cria sem equipe se for a primeira
                // vez que esse escrevente aparece. Sem isso, EscreventeId apontaria pra uma
                // linha que não existe (FK quebrada na hora de gravar o protocolo).
                var escrevente = (await escreventes.ObterTodosAsync(cancellationToken))
                    .FirstOrDefault(e => string.Equals(e.Nome, request.EscreventeNome, StringComparison.OrdinalIgnoreCase));
                if (escrevente is null)
                {
                    escrevente = new Escrevente(Guid.NewGuid(), request.EscreventeNome, equipeId: null);
                    escreventes.Adicionar(escrevente);
                }

                // Endpoint avulso, sem relatório por trás — usa "agora" como o instante do
                // andamento, já que não existe um de verdade vindo de importação nenhuma.
                var protocolo = new Protocolo(
                    Guid.NewGuid(), request.Numero, tipoAtoId, escrevente.Id, request.Etapa, relogio.Agora, request.Prioridade);

                var resultado = await casoDeUso.ExecutarAsync(protocolo, escrevente, cancellationToken);

                return Results.Ok(ParaResponse(protocolo, resultado));
            })
            .WithName("DistribuirProtocolo")
            .WithSummary("Resolve o prazo do protocolo e decide o destino: atribuído, pool ou exceção.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces<DistribuirProtocoloResponse>()
            // Seção 3 do requisito: importação/distribuição é ação de gestão — só Distribuidora.
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/redistribuir-pool", async (RedistribuirPool casoDeUso, CancellationToken cancellationToken) =>
            {
                var alterados = await casoDeUso.ExecutarAsync(cancellationToken);
                return Results.Ok(new RedistribuirPoolResponse(alterados));
            })
            .WithName("RedistribuirPool")
            .WithSummary("Reaplica o motor a todo protocolo sem dono (pool ou exceção) — RF-16.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces<RedistribuirPoolResponse>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/{id:guid}/atribuir", async (
                Guid id,
                AtribuirManualmenteRequest request,
                AtribuirManualmente casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, request.ConferenteId, cancellationToken);
                return resultado switch
                {
                    ResultadoAtribuirManualmente.Sucesso => Results.NoContent(),
                    ResultadoAtribuirManualmente.ProtocoloNaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoAtribuirManualmente.ConferenteNaoEncontrado => Results.NotFound(new { motivo = "conferente não encontrado" }),
                    ResultadoAtribuirManualmente.ProtocoloNaoEstaEmExcecao => Results.Conflict(new { motivo = "protocolo não está em exceção" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("AtribuirProtocoloManualmente")
            .WithSummary("Resolve uma exceção atribuindo na mão, sem passar pelo motor (RF-17).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/{id:guid}/descartar", async (Guid id, DescartarExcecao casoDeUso, CancellationToken cancellationToken) =>
            {
                var encontrado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return encontrado ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DescartarExcecaoProtocolo")
            .WithSummary("Descarta uma exceção sem resolver (RF-17). Só funciona em protocolo que está em exceção.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPut("/protocolos/{id:guid}/observacao", async (
                Guid id,
                DefinirObservacaoRequest request,
                DefinirObservacao casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var encontrado = await casoDeUso.ExecutarAsync(id, request.Observacao, cancellationToken);
                return encontrado ? Results.NoContent() : Results.NotFound();
            })
            .WithName("DefinirObservacaoProtocolo")
            .WithSummary("Define a observação do protocolo, visível pra gestão (RF-15/RF-23). Editável em qualquer estado.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            // TODO: quando "Minha fila" existir, o próprio conferente dono do protocolo também
            // precisa conseguir chamar isso (RF-23) — hoje só Distribuidora, por simplicidade.
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
    string EscreventeNome);

public sealed record DistribuirProtocoloResponse(
    Guid ProtocoloId,
    string Resultado,
    Guid? ConferenteId,
    string? Motivo,
    DateTimeOffset? VencimentoEm);

public sealed record RedistribuirPoolResponse(int Alterados);

public sealed record AtribuirManualmenteRequest(Guid ConferenteId);

public sealed record DefinirObservacaoRequest(string? Observacao);
