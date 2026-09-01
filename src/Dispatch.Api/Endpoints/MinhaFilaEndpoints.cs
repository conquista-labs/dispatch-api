using System.Security.Claims;
using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

// Primeiro grupo de endpoints exclusivo do papel Conferente (RF-19 a RF-24) — "quem sou eu"
// nunca é um parâmetro de request, sempre resolvido do token (RNF-04: a restrição de dono é
// sempre no servidor).
public static class MinhaFilaEndpoints
{
    // Mesmas faixas hardcoded do RF-14 (DistribuicaoEndpoints) — ainda sem tabela `config`
    // (seção 8). Duplicado aqui de propósito: é configuração, não lógica, e as duas telas
    // podem divergir de faixa no futuro sem acoplar uma na outra.
    private static readonly TimeSpan FaixaAtencao = TimeSpan.FromHours(4);
    private static readonly TimeSpan FaixaUrgente = TimeSpan.FromMinutes(60);

    public static void MapMinhaFilaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/minha-fila")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Conferente)))
            .WithTags(OpenApiTags.MinhaFila);

        grupo.MapGet("/", async (
                ObterMinhaFila casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                IRelogio relogio,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var fila = await casoDeUso.ExecutarAsync(conferente, cancellationToken);
                var agora = relogio.Agora;
                return Results.Ok(new MinhaFilaResponse(
                    fila.PoolDisponivel.Select(p => ParaResumo(p, agora)).ToList(),
                    fila.Atribuidos.Select(p => ParaResumo(p, agora)).ToList(),
                    fila.EmConferencia.Select(p => ParaResumo(p, agora)).ToList()));
            })
            .WithName("ObterMinhaFila")
            .WithSummary("As três colunas do conferente: pool disponível (já filtrado pela alçada), atribuídos e em conferência (RF-19).")
            .Produces<MinhaFilaResponse>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapPost("/{id:guid}/pegar", async (
                Guid id,
                PegarProtocolo casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var resultado = await casoDeUso.ExecutarAsync(id, conferente, cancellationToken);
                return resultado switch
                {
                    ResultadoPegarProtocolo.Sucesso => Results.NoContent(),
                    ResultadoPegarProtocolo.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoPegarProtocolo.NaoEstaNoPool => Results.Conflict(new { motivo = "protocolo não está no pool" }),
                    ResultadoPegarProtocolo.SemAlcada => Results.Forbid(),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("PegarProtocolo")
            .WithSummary("Pega um protocolo do pool pra si — só funciona se estiver no pool e dentro da alçada do conferente (RF-20).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict)
            .Produces(StatusCodes.Status403Forbidden);

        grupo.MapPost("/{id:guid}/iniciar", async (
                Guid id,
                IniciarConferencia casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var resultado = await casoDeUso.ExecutarAsync(id, conferente, cancellationToken);
                return resultado switch
                {
                    ResultadoIniciarConferencia.Sucesso => Results.NoContent(),
                    ResultadoIniciarConferencia.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoIniciarConferencia.NaoEhSeuOuNaoEstaAtribuido =>
                        Results.Conflict(new { motivo = "protocolo não é seu ou não está atribuído" }),
                    ResultadoIniciarConferencia.LimiteDeSimultaneosAtingido =>
                        Results.Conflict(new { motivo = "limite de atos simultâneos atingido" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("IniciarConferencia")
            .WithSummary("Arranca o cronômetro de um protocolo atribuído ao próprio conferente, respeitando o limite de simultâneos (RF-21).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/concluir", async (
                Guid id,
                ConcluirConferenciaRequest request,
                ConcluirConferencia casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var resultado = await casoDeUso.ExecutarAsync(id, conferente, request.Aprovado, cancellationToken);
                return resultado switch
                {
                    ResultadoConcluirConferencia.Sucesso => Results.NoContent(),
                    ResultadoConcluirConferencia.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoConcluirConferencia.NaoEhSeuOuNaoEstaEmConferencia =>
                        Results.Conflict(new { motivo = "protocolo não é seu ou não está em conferência" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("ConcluirConferencia")
            .WithSummary("Aprova ou reprova o ato, encerrando a conferência e gravando a duração (RF-22).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapGet("/concluidos-hoje", async (
                ObterConcluidosHoje casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                IPedidoReaberturaRepository pedidos,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var concluidos = await casoDeUso.ExecutarAsync(conferente, cancellationToken);
                // ToDictionary<Guid, Guid?> de propósito — com Dictionary<Guid, Guid> puro,
                // GetValueOrDefault devolveria Guid.Empty (não null) pra quem não tem pedido,
                // e o front receberia um "00000000-..." em vez de null.
                var pendentesPorProtocolo = (await pedidos.ObterPendentesPorProtocolosAsync(
                        concluidos.Select(p => p.Id).ToList(), cancellationToken))
                    .ToDictionary(p => p.ProtocoloId, Guid? (p) => p.Id);
                return Results.Ok(concluidos.Select(p => ParaResumoConcluido(p, pendentesPorProtocolo.GetValueOrDefault(p.Id))).ToList());
            })
            .WithName("ObterConcluidosHoje")
            .WithSummary("Indicadores do dia: protocolos aprovados/reprovados hoje pelo próprio conferente, com duração (RF-24).")
            .Produces<IReadOnlyList<ProtocoloConcluidoResumo>>();

        grupo.MapPost("/{id:guid}/corrigir-resultado", async (
                Guid id,
                CorrigirResultado casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var resultado = await casoDeUso.ExecutarAsync(id, conferente, cancellationToken);
                return resultado switch
                {
                    ResultadoCorrigirResultado.Sucesso => Results.NoContent(),
                    ResultadoCorrigirResultado.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoCorrigirResultado.NaoEhSeu => Results.Forbid(),
                    ResultadoCorrigirResultado.StatusInvalido => Results.Conflict(new { motivo = "protocolo não está concluído" }),
                    ResultadoCorrigirResultado.ForaDaJanela => Results.Conflict(new { motivo = "janela de correção encerrada" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CorrigirResultado")
            .WithSummary($"Troca aprovado↔reprovado dentro de {CorrigirResultado.JanelaDeCorrecao.TotalMinutes:0} min depois de concluído (RF-24a).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/pedir-reabertura", async (
                Guid id,
                PedirReabertura casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var resultado = await casoDeUso.ExecutarAsync(id, conferente, cancellationToken);
                return resultado switch
                {
                    ResultadoPedirReabertura.Sucesso sucesso => Results.Created($"/minha-fila/pedidos-reabertura/{sucesso.PedidoId}", new PedirReaberturaResponse(sucesso.PedidoId)),
                    ResultadoPedirReabertura.ProtocoloNaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoPedirReabertura.NaoEhSeu => Results.Forbid(),
                    ResultadoPedirReabertura.StatusInvalido => Results.Conflict(new { motivo = "protocolo não está concluído" }),
                    ResultadoPedirReabertura.JaExistePedidoPendente => Results.Conflict(new { motivo = "já existe um pedido pendente para este protocolo" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("PedirReabertura")
            .WithSummary("Abre pedido de reabertura pra distribuidora decidir — fora da janela de correção (RF-24b).")
            .Produces<PedirReaberturaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPost("/pedidos-reabertura/{id:guid}/cancelar", async (
                Guid id,
                CancelarPedidoReabertura casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                CancellationToken cancellationToken) =>
            {
                var conferente = await ResolverConferenteAsync(usuario, conferentes, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var resultado = await casoDeUso.ExecutarAsync(id, conferente, cancellationToken);
                return resultado switch
                {
                    ResultadoCancelarPedidoReabertura.Sucesso => Results.NoContent(),
                    ResultadoCancelarPedidoReabertura.NaoEncontrado => Results.NotFound(new { motivo = "pedido não encontrado" }),
                    ResultadoCancelarPedidoReabertura.NaoEhSeu => Results.Forbid(),
                    ResultadoCancelarPedidoReabertura.NaoEstaPendente => Results.Conflict(new { motivo = "pedido não está pendente" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CancelarPedidoReabertura")
            .WithSummary("Cancela um pedido de reabertura — só enquanto pendente (RF-24b).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status409Conflict);
    }

    // JWT carrega Usuario.Id (NameIdentifier), não Conferente.Id — toda ação de "Minha fila"
    // começa resolvendo um a partir do outro.
    private static async Task<Conferente?> ResolverConferenteAsync(
        ClaimsPrincipal usuario, IConferenteRepository conferentes, CancellationToken cancellationToken)
    {
        var usuarioId = Guid.Parse(usuario.FindFirstValue(ClaimTypes.NameIdentifier)!);
        return await conferentes.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);
    }

    // internal, não private: ConferenteEndpoints reaproveita (GET /conferentes/{id}/fila —
    // Distribuidora vendo a fila de um conferente específico, em leitura) — é mapeamento de
    // verdade (Protocolo → DTO), diferente das faixas hardcoded acima, que são config e por
    // isso ficam duplicadas de propósito.
    internal static ProtocoloResumo ParaResumo(Protocolo protocolo, DateTimeOffset agora) => new(
        protocolo.Id,
        protocolo.Numero,
        protocolo.TipoAtoId,
        protocolo.EscreventeId,
        protocolo.Etapa,
        protocolo.Prioridade,
        protocolo.Status,
        protocolo.DonoId,
        protocolo.VencimentoEm,
        protocolo.MotivoExcecao,
        protocolo.Observacao,
        protocolo.VencimentoEm is { } vencimento ? Semaforo.Calcular(vencimento, agora, FaixaAtencao, FaixaUrgente) : null,
        protocolo.IniciadoEm);

    internal static ProtocoloConcluidoResumo ParaResumoConcluido(Protocolo protocolo, Guid? pedidoReaberturaPendenteId) => new(
        protocolo.Id,
        protocolo.Numero,
        protocolo.TipoAtoId,
        protocolo.Etapa,
        protocolo.Status,
        protocolo.ConcluidoEm,
        protocolo.Duracao,
        protocolo.CorrigidoEm,
        pedidoReaberturaPendenteId);
}

public sealed record MinhaFilaResponse(
    IReadOnlyList<ProtocoloResumo> PoolDisponivel,
    IReadOnlyList<ProtocoloResumo> Atribuidos,
    IReadOnlyList<ProtocoloResumo> EmConferencia);

public sealed record ConcluirConferenciaRequest(bool Aprovado);

public sealed record PedirReaberturaResponse(Guid PedidoId);

public sealed record ProtocoloConcluidoResumo(
    Guid Id,
    string Numero,
    Guid? TipoAtoId,
    Etapa Etapa,
    StatusProtocolo Status,
    DateTimeOffset? ConcluidoEm,
    TimeSpan? Duracao,
    DateTimeOffset? CorrigidoEm,
    Guid? PedidoReaberturaPendenteId);
