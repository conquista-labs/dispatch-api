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
    public static void MapMinhaFilaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/minha-fila")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Conferente)))
            .WithTags(OpenApiTags.MinhaFila);

        grupo.MapGet("/", async (
                ObterMinhaFila casoDeUso,
                ClaimsPrincipal usuario,
                IConferenteRepository conferentes,
                ObterConfiguracao obterConfiguracao,
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
                var config = await obterConfiguracao.ExecutarAsync(cancellationToken);
                // ADR-0046: na fila do próprio conferente, o escrevente (e por ele a equipe) só aparece
                // no que já está em conferência — pool e atribuídas saem sem, pra ninguém escolher o
                // ato por quem fez. A visão de gestão (GET /conferentes/{id}/fila) continua com tudo.
                return Results.Ok(ParaFilaResponse(fila, agora, config, ocultarEscreventeAntesDeConferir: true));
            })
            .WithName("ObterMinhaFila")
            .WithSummary("As três colunas do conferente: pool disponível (alçada + ordem da vez), atribuídos e em conferência (RF-19), e a regra do pool (ordem obrigatória, limite na mão, próximo da vez). escreventeId sai null no pool e nas atribuídas.")
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
                // `codigo` só nos dois desfechos novos (ADR-0046): o front reage diferente a cada um. Os
                // de antes ficam exatamente como estavam (status e corpo), o front em produção lê assim.
                return resultado switch
                {
                    ResultadoPegarProtocolo.Sucesso => Results.NoContent(),
                    ResultadoPegarProtocolo.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoPegarProtocolo.NaoEstaNoPool => Results.Conflict(new { motivo = "protocolo não está no pool" }),
                    ResultadoPegarProtocolo.SemAlcada => Results.Forbid(),
                    ResultadoPegarProtocolo.LimiteNaMao limite => Results.Conflict(new
                    {
                        codigo = "limite_na_mao",
                        motivo = $"você já tem {limite.NaMao} {(limite.NaMao == 1 ? "ato" : "atos")} na mão (limite {limite.Limite}) — conclua algum antes de pegar outro"
                    }),
                    ResultadoPegarProtocolo.ForaDaVez => Results.Conflict(new
                    {
                        codigo = "fora_da_vez",
                        motivo = "é preciso pegar o primeiro da fila do pool"
                    }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("PegarProtocolo")
            .WithSummary("Pega um protocolo do pool pra si (RF-20): precisa estar no pool, na alçada, com a mão abaixo do limite (409 limite_na_mao) e, com a ordem obrigatória ligada, ser o primeiro da vez (409 fora_da_vez).")
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

        grupo.MapPost("/{id:guid}/pausar", async (
                Guid id,
                PausarConferencia casoDeUso,
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
                    ResultadoPausarConferencia.Sucesso => Results.NoContent(),
                    ResultadoPausarConferencia.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoPausarConferencia.NaoEhSeuOuNaoEstaEmConferencia =>
                        Results.Conflict(new { motivo = "protocolo não é seu ou não está em conferência" }),
                    ResultadoPausarConferencia.JaEstaPausado => Results.Conflict(new { motivo = "protocolo já está pausado" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("PausarConferencia")
            .WithSummary("Congela o cronômetro sem devolver o ato pra fila — continua ocupando o limite de simultâneos (RF-21).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/retomar", async (
                Guid id,
                RetomarConferencia casoDeUso,
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
                    ResultadoRetomarConferencia.Sucesso => Results.NoContent(),
                    ResultadoRetomarConferencia.NaoEncontrado => Results.NotFound(new { motivo = "protocolo não encontrado" }),
                    ResultadoRetomarConferencia.NaoEhSeuOuNaoEstaEmConferencia =>
                        Results.Conflict(new { motivo = "protocolo não é seu ou não está em conferência" }),
                    ResultadoRetomarConferencia.NaoEstaPausado => Results.Conflict(new { motivo = "protocolo não está pausado" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado}")
                };
            })
            .WithName("RetomarConferencia")
            .WithSummary("Volta a contar o tempo de um protocolo pausado, abrindo um novo ciclo a partir de agora.")
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
                    ResultadoConcluirConferencia.EstaPausado => Results.Conflict(new { motivo = "protocolo está pausado — retome antes de concluir" }),
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
            .WithSummary("Troca aprovado↔reprovado dentro da janela de correção configurada, depois de concluído (RF-24a).")
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
        var usuarioId = usuario.ObterUsuarioId();
        return await conferentes.ObterPorUsuarioIdAsync(usuarioId, cancellationToken);
    }

    // internal, não private: ConferenteEndpoints reaproveita (GET /conferentes/{id}/fila —
    // Distribuidora vendo a fila de um conferente específico, em leitura) e DistribuicaoEndpoints
    // também (RF-14 — antes tinha uma cópia própria, unificado numa auditoria de qualidade) —
    // é mapeamento de verdade (Protocolo → DTO). Faixas do semáforo entram como parâmetro
    // (tabela `config`, seção 8) em vez de campo estático — cada chamador busca a config uma
    // vez por request via ObterConfiguracao.
    internal static ProtocoloResumo ParaResumo(
        Protocolo protocolo, DateTimeOffset agora, TimeSpan faixaAtencao, TimeSpan faixaUrgente, int numeroDaConferencia,
        bool ocultarEscrevente = false) => new(
        protocolo.Id,
        protocolo.Numero,
        protocolo.TipoAtoId,
        protocolo.TipoAtoNomeOriginal,
        ocultarEscrevente ? null : protocolo.EscreventeId,
        protocolo.Etapa,
        protocolo.Prioridade,
        protocolo.Status,
        protocolo.DonoId,
        protocolo.VencimentoEm,
        protocolo.MotivoExcecao,
        protocolo.Observacao,
        protocolo.VencimentoEm is { } vencimento ? Semaforo.Calcular(vencimento, agora, faixaAtencao, faixaUrgente) : null,
        protocolo.IniciadoEm,
        protocolo.PausadoEm,
        protocolo.ConcluidoEm,
        protocolo.Duracao,
        protocolo.AndamentoEm,
        numeroDaConferencia);

    // As duas leituras de fila (a do próprio conferente e a de gestão) montam a resposta aqui, pra que
    // `regraDoPool` e a ordem saiam iguais nas duas — só o corte do escrevente difere.
    internal static MinhaFilaResponse ParaFilaResponse(
        MinhaFila fila, DateTimeOffset agora, Configuracao config, bool ocultarEscreventeAntesDeConferir)
    {
        ProtocoloResumo Resumo(Protocolo p, bool ocultarEscrevente) => ParaResumo(
            p, agora, config.FaixaAtencao, config.FaixaUrgente, fila.NumeroDaConferencia.GetValueOrDefault(p.Id, 1), ocultarEscrevente);

        return new MinhaFilaResponse(
            fila.PoolDisponivel.Select(p => Resumo(p, ocultarEscreventeAntesDeConferir)).ToList(),
            fila.Atribuidos.Select(p => Resumo(p, ocultarEscreventeAntesDeConferir)).ToList(),
            fila.EmConferencia.Select(p => Resumo(p, ocultarEscrevente: false)).ToList(),
            ParaFaixas(config),
            new RegraDoPoolResponse(
                fila.RegraDoPool.OrdemObrigatoria, fila.RegraDoPool.LimiteNaMao, fila.RegraDoPool.NaMao, fila.RegraDoPool.ProximoId));
    }

    // Mesmas faixas que o Semaforo de cada item usou — a legenda "Prazo do ato" da Minha fila
    // mostra os limites de verdade, e o Conferente puro não lê GET /config (é só gestão).
    internal static FaixasSemaforoResponse ParaFaixas(Configuracao config) =>
        new((int)config.FaixaAtencao.TotalMinutes, (int)config.FaixaUrgente.TotalMinutes);

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
    IReadOnlyList<ProtocoloResumo> EmConferencia,
    FaixasSemaforoResponse Faixas,
    RegraDoPoolResponse RegraDoPool);

// ADR-0046. naMao = atribuídos + em conferência. proximoId = o primeiro do pool disponível na ordem da
// vez enquanto naMao < limiteNaMao (senão null) — vem preenchido mesmo com ordemObrigatoria false (o
// campo quer dizer "o próximo da vez"; com a chave desligada o front pode ignorar).
public sealed record RegraDoPoolResponse(bool OrdemObrigatoria, int LimiteNaMao, int NaMao, Guid? ProximoId);

// Limites do semáforo da Configuração (seção 8) em minutos inteiros, como GET /config expõe.
public sealed record FaixasSemaforoResponse(int AtencaoMinutos, int UrgenteMinutos);

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
