using System.Security.Claims;
using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class ProtocoloEndpoints
{
    // Mesmos limiares hardcoded de DistribuicaoEndpoints (RF-14) — ainda não é configuração de
    // sistema de verdade (RF-30c fala nisso), só constante duplicada até existir uma tabela
    // de configuração.
    private static readonly TimeSpan FaixaAtencao = TimeSpan.FromHours(4);
    private static readonly TimeSpan FaixaUrgente = TimeSpan.FromMinutes(60);

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
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                // RF-15 (Distribuidora, sem restrição) e RF-23 (o próprio conferente dono,
                // restrito) são o mesmo endpoint — o papel do token decide se conferenteRestritoId
                // vai preenchido ou nulo.
                Guid? conferenteRestritoId = null;
                if (usuario.IsInRole(nameof(Papel.Conferente)))
                {
                    var usuarioId = Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);
                    var conferente = await conferentes.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);
                    if (conferente is null)
                    {
                        return Results.NotFound(new { motivo = "conferente não encontrado" });
                    }

                    conferenteRestritoId = conferente.Id;
                }

                var resultado = await casoDeUso.ExecutarAsync(id, request.Observacao, conferenteRestritoId, cancellationToken);
                return resultado switch
                {
                    ResultadoDefinirObservacao.Sucesso => Results.NoContent(),
                    ResultadoDefinirObservacao.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoDefinirObservacao.NaoEhSeu => Results.Forbid(),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("DefinirObservacaoProtocolo")
            .WithSummary("Define a observação do protocolo (RF-15/RF-23). Editável em qualquer estado. Distribuidora não tem restrição; Conferente só edita protocolo do qual é dono.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora), nameof(Papel.Conferente)));

        app.MapGet("/protocolos/{id:guid}/detalhe", async (Guid id, ObterDetalheProtocolo casoDeUso, IRelogio relogio, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return resultado is null
                    ? Results.NotFound(new { motivo = "protocolo não encontrado" })
                    : Results.Ok(ParaDetalheResponse(resultado, relogio.Agora));
            })
            .WithName("ObterDetalheProtocolo")
            .WithSummary("Painel de detalhe (RF-18a) — todos os campos do protocolo, mais quem pode conferir este ato especificamente.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces<DetalheProtocoloResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/{id:guid}/devolver-ao-pool", async (Guid id, DevolverAoPool casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return resultado switch
                {
                    ResultadoDevolverAoPool.Sucesso => Results.NoContent(),
                    ResultadoDevolverAoPool.ProtocoloNaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoDevolverAoPool.ProtocoloNaoEstaAtribuido => Results.Conflict(new { motivo = "protocolo não está atribuído" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("DevolverProtocoloAoPool")
            .WithSummary("Devolve um protocolo atribuído pro pool (RF-18a) — ação pontual num item só, diferente de redistribuir-pool (RF-16, em lote).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/{id:guid}/atribuir-ao-menos-carregado", async (
                Guid id, AtribuirAoMenosCarregado casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return resultado switch
                {
                    ResultadoAtribuirAoMenosCarregado.Sucesso => Results.NoContent(),
                    ResultadoAtribuirAoMenosCarregado.ProtocoloNaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoAtribuirAoMenosCarregado.ProtocoloNaoElegivel => Results.Conflict(new { motivo = "protocolo não está no pool nem em exceção" }),
                    ResultadoAtribuirAoMenosCarregado.NinguemComAlcada => Results.Conflict(new { motivo = "ninguém com alçada na escala" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("AtribuirAoMenosCarregado")
            .WithSummary("Atribui a quem tem alçada e está com menos carga agora (RF-18a) — não exige exceção, diferente de atribuir manualmente (RF-17).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/{id:guid}/definir-prioridade", async (
                Guid id, DefinirPrioridadeRequest request, DefinirPrioridadeDoProtocolo casoDeUso, CancellationToken cancellationToken) =>
                await casoDeUso.ExecutarAsync(id, request.Prioridade, cancellationToken) ? Results.NoContent() : Results.NotFound())
            .WithName("DefinirPrioridadeDoProtocolo")
            .WithSummary("Marca/desmarca um protocolo como urgente — único jeito real de definir prioridade alta hoje (a importação nunca define).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/{id:guid}/reabrir-conferencia", async (Guid id, ReabrirConferencia casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, cancellationToken);
                return resultado switch
                {
                    ResultadoReabrirConferencia.Sucesso => Results.NoContent(),
                    ResultadoReabrirConferencia.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoReabrirConferencia.StatusInvalido => Results.Conflict(new { motivo = "protocolo não está concluído" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("ReabrirConferenciaDoProtocolo")
            .WithSummary("Ação direta do painel de detalhe (RF-18a/RF-24c) — reabre sem exigir pedido explícito do conferente.")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapGet("/protocolos/pedidos-reabertura", async (ListarPedidosReaberturaPendentes casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok((await casoDeUso.ExecutarAsync(cancellationToken)).Select(ParaPedidoReaberturaResponse).ToList()))
            .WithName("ListarPedidosReaberturaPendentes")
            .WithSummary("Pedidos de reabertura pendentes, pra seção própria da aba de Exceções (RF-24c).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces<IReadOnlyList<PedidoReaberturaResponse>>()
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/pedidos-reabertura/{id:guid}/aprovar", async (
                Guid id, DecidirPedidoReabertura casoDeUso, ClaimsPrincipal usuario, CancellationToken cancellationToken) =>
                await ExecutarDecisaoAsync(id, aprovar: true, casoDeUso, usuario, cancellationToken))
            .WithName("AprovarPedidoReabertura")
            .WithSummary("Reabre o protocolo (mesmo dono, cronômetro do zero) e marca o pedido como aprovado (RF-24c).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));

        app.MapPost("/protocolos/pedidos-reabertura/{id:guid}/negar", async (
                Guid id, DecidirPedidoReabertura casoDeUso, ClaimsPrincipal usuario, CancellationToken cancellationToken) =>
                await ExecutarDecisaoAsync(id, aprovar: false, casoDeUso, usuario, cancellationToken))
            .WithName("NegarPedidoReabertura")
            .WithSummary("Nega o pedido — o protocolo não muda (RF-24c).")
            .WithTags(OpenApiTags.Protocolos)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)));
    }

    private static async Task<IResult> ExecutarDecisaoAsync(
        Guid pedidoId, bool aprovar, DecidirPedidoReabertura casoDeUso, ClaimsPrincipal usuario, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var resultado = await casoDeUso.ExecutarAsync(pedidoId, aprovar, usuarioId, cancellationToken);
        return resultado switch
        {
            ResultadoDecidirPedidoReabertura.Sucesso => Results.NoContent(),
            ResultadoDecidirPedidoReabertura.NaoEncontrado => Results.NotFound(new { motivo = "pedido não encontrado" }),
            ResultadoDecidirPedidoReabertura.NaoEstaPendente => Results.Conflict(new { motivo = "pedido não está pendente" }),
            _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
        };
    }

    private static PedidoReaberturaResponse ParaPedidoReaberturaResponse(PedidoReaberturaResumo r) => new(
        r.PedidoId, r.ProtocoloId, r.ProtocoloNumero, r.TipoAtoId, r.Etapa, r.StatusAtual, r.SolicitanteId, r.NomeSolicitante, r.CriadoEm);

    private static DetalheProtocoloResponse ParaDetalheResponse(ResultadoDetalheProtocolo resultado, DateTimeOffset agora)
    {
        var p = resultado.Protocolo;
        return new DetalheProtocoloResponse(
            p.Id, p.Numero, p.TipoAtoId, p.TipoAtoNomeOriginal, p.EscreventeId, p.Etapa, p.Prioridade, p.AndamentoEm,
            p.Prazo?.Tipo, p.VencimentoEm, p.Status, p.DonoId, p.MotivoExcecao, p.Observacao,
            p.AtribuidoEm, p.IniciadoEm, p.ConcluidoEm, p.RegraAplicadaId, p.CorrigidoEm, p.ReabertoEm,
            p.VencimentoEm is { } vencimento ? Semaforo.Calcular(vencimento, agora, FaixaAtencao, FaixaUrgente) : null,
            resultado.Avaliacoes.Select(a => new AlcadaConferenteResponse(
                a.Conferente.Id, a.Elegivel, a.DecisaoEtapa.RegraAplicada?.Id, a.DecisaoTipo.RegraAplicada?.Id)).ToList());
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

public sealed record DefinirPrioridadeRequest(Prioridade Prioridade);

public sealed record DetalheProtocoloResponse(
    Guid Id,
    string Numero,
    Guid? TipoAtoId,
    string? TipoAtoNomeOriginal,
    Guid EscreventeId,
    Etapa Etapa,
    Prioridade Prioridade,
    DateTimeOffset AndamentoEm,
    TipoPrazo? Prazo,
    DateTimeOffset? VencimentoEm,
    StatusProtocolo Status,
    Guid? DonoId,
    string? MotivoExcecao,
    string? Observacao,
    DateTimeOffset? AtribuidoEm,
    DateTimeOffset? IniciadoEm,
    DateTimeOffset? ConcluidoEm,
    Guid? RegraAplicadaId,
    DateTimeOffset? CorrigidoEm,
    DateTimeOffset? ReabertoEm,
    FaixaSemaforo? Semaforo,
    IReadOnlyList<AlcadaConferenteResponse> Alcada);

// RegraEtapaId/RegraTipoId nulos não significam "sem alçada" — podem vir do padrão aberto
// (ausência de regra = permitido). O front resolve `Elegivel` já pronto; as duas regras só
// servem pra mostrar "por qual regra" quando existir uma.
public sealed record AlcadaConferenteResponse(Guid ConferenteId, bool Elegivel, Guid? RegraEtapaId, Guid? RegraTipoId);

public sealed record PedidoReaberturaResponse(
    Guid PedidoId,
    Guid ProtocoloId,
    string ProtocoloNumero,
    Guid? TipoAtoId,
    Etapa Etapa,
    StatusProtocolo StatusAtual,
    Guid SolicitanteId,
    string NomeSolicitante,
    DateTimeOffset CriadoEm);
