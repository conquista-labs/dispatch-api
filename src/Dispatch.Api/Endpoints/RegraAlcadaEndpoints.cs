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

        grupo.MapGet("/", async (ListarRegrasAlcada casoDeUso, IProtocoloRepository protocolos, CancellationToken cancellationToken) =>
            {
                var todas = await casoDeUso.ExecutarAsync(cancellationToken);
                var respostas = new List<RegraAlcadaResponse>();
                foreach (var regra in todas)
                {
                    var usos = await protocolos.ContarComRegraAplicadaAsync(regra.Id, cancellationToken);
                    respostas.Add(ParaResponse(regra, usos));
                }
                return Results.Ok(respostas);
            })
            .WithName("ListarRegrasAlcada")
            .WithSummary("Lista todas as regras de alçada, ativas e inativas, com o contador de aplicações (RF-33).")
            .Produces<IReadOnlyList<RegraAlcadaResponse>>();

        grupo.MapPost("/", async (CriarRegraAlcadaRequest request, CriarRegraAlcada casoDeUso, CancellationToken cancellationToken) =>
            {
                var (sujeito, erroSujeito) = TentarMontarSujeito(request);
                if (sujeito is null)
                {
                    return Results.BadRequest(new { motivo = erroSujeito });
                }

                var (alvo, erroAlvo) = TentarMontarAlvo(request);
                if (alvo is null)
                {
                    return Results.BadRequest(new { motivo = erroAlvo });
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
                    ResultadoCriarRegraAlcada.EquipeNaoEncontrada =>
                        Results.NotFound(new { motivo = "equipe não encontrada" }),
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

        grupo.MapPost("/testar", async (TestarAlcadaRequest request, SimularAlcada casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(request.Etapa, request.TipoAtoId, request.EquipeId, request.Prioridade, cancellationToken);
                if (resultado is null)
                {
                    return Results.NotFound(new { motivo = "tipo de ato não encontrado" });
                }

                return Results.Ok(new TestarAlcadaResponse(
                    resultado.Avaliacoes.Select(a => new AlcadaConferenteResponse(
                        a.Conferente.Id, a.Elegivel, a.Decisao.RegraAplicada?.Id, a.Decisao.Motivo,
                        a.Trilha.Select(t => new PassoTrilhaResponse(t.Camada, t.Efeito, t.Regra?.Id)).ToList())).ToList(),
                    resultado.Destino, resultado.ConferenteId, resultado.Motivo));
            })
            .WithName("TestarAlcada")
            .WithSummary("Simulador \"Testar\" da aba Alçada — quem pode/não pode conferir um caso hipotético e por quê.")
            .Produces<TestarAlcadaResponse>()
            .Produces(StatusCodes.Status404NotFound);
    }

    private static (SujeitoAlcada? Sujeito, string? Erro) TentarMontarSujeito(CriarRegraAlcadaRequest request) => request switch
    {
        { SujeitoNivel: { } nivel, SujeitoConferenteId: null } => (new SujeitoAlcada.PorNivel(nivel), null),
        { SujeitoConferenteId: { } conferenteId, SujeitoNivel: null } => (new SujeitoAlcada.PorPessoa(conferenteId), null),
        _ => (null, "informe exatamente um entre sujeitoNivel e sujeitoConferenteId")
    };

    // "Equipe" aceita Guid? nulo como valor válido (RF-29a: "sem equipe" é alvo legítimo) — por
    // isso o XOR usa um flag próprio (AlvoEhEquipe) em vez de só checar AlvoEquipeId != null,
    // senão não daria pra diferenciar "regra de equipe = sem equipe" de "não é regra de
    // equipe". As 5 variantes de AlvoAlcada ficam num array só (contagem + construção juntas)
    // pra não correr o risco de uma 6ª variante ser adicionada num lugar e esquecida no outro
    // (achado numa auditoria de qualidade — antes eram um array de contagem e um switch de
    // construção separados, cada um enumerando as mesmas 5 variantes por conta própria).
    private static (AlvoAlcada? Alvo, string? Erro) TentarMontarAlvo(CriarRegraAlcadaRequest request)
    {
        (bool Informado, Func<AlvoAlcada> Criar)[] candidatos =
        [
            (request.AlvoEtapa is not null, () => new AlvoAlcada.PorEtapa(request.AlvoEtapa!.Value)),
            (request.AlvoTipoAtoId is not null, () => new AlvoAlcada.PorTipoAto(request.AlvoTipoAtoId!.Value)),
            (request.AlvoEhEquipe, () => new AlvoAlcada.PorEquipeDeEscrevente(request.AlvoEquipeId)),
            (request.AlvoTodosOsAtos, () => new AlvoAlcada.PorTodosOsAtos()),
            (request.AlvoGrupo is not null, () => new AlvoAlcada.PorGrupoTipoAto(request.AlvoGrupo!.Value))
        ];

        var informados = candidatos.Where(c => c.Informado).ToList();
        return informados.Count == 1
            ? (informados[0].Criar(), null)
            : (null, "informe exatamente um entre alvoEtapa, alvoTipoAtoId, alvoEhEquipe, alvoTodosOsAtos e alvoGrupo");
    }

    private static RegraAlcadaResponse ParaResponse(RegraAlcada regra, int usos) => new(
        regra.Id,
        (regra.Sujeito as SujeitoAlcada.PorNivel)?.Nivel,
        (regra.Sujeito as SujeitoAlcada.PorPessoa)?.ConferenteId,
        regra.Permissao,
        (regra.Alvo as AlvoAlcada.PorEtapa)?.Etapa,
        (regra.Alvo as AlvoAlcada.PorTipoAto)?.TipoAtoId,
        regra.Alvo is AlvoAlcada.PorEquipeDeEscrevente,
        (regra.Alvo as AlvoAlcada.PorEquipeDeEscrevente)?.EquipeId,
        regra.Alvo is AlvoAlcada.PorTodosOsAtos,
        (regra.Alvo as AlvoAlcada.PorGrupoTipoAto)?.Grupo,
        regra.Origem,
        regra.Ativa,
        usos);
}

public sealed record CriarRegraAlcadaRequest(
    Nivel? SujeitoNivel,
    Guid? SujeitoConferenteId,
    PermissaoRegra Permissao,
    Etapa? AlvoEtapa,
    Guid? AlvoTipoAtoId,
    bool AlvoEhEquipe,
    Guid? AlvoEquipeId,
    bool AlvoTodosOsAtos,
    GrupoTipoAto? AlvoGrupo);

public sealed record CriarRegraAlcadaResponse(Guid RegraId);

public sealed record RegraAlcadaResponse(
    Guid Id,
    Nivel? SujeitoNivel,
    Guid? SujeitoConferenteId,
    PermissaoRegra Permissao,
    Etapa? AlvoEtapa,
    Guid? AlvoTipoAtoId,
    bool AlvoEhEquipe,
    Guid? AlvoEquipeId,
    bool AlvoTodosOsAtos,
    GrupoTipoAto? AlvoGrupo,
    OrigemRegra Origem,
    bool Ativa,
    int Usos);

public sealed record TestarAlcadaRequest(Etapa Etapa, Guid TipoAtoId, Guid? EquipeId, Prioridade Prioridade);

public sealed record TestarAlcadaResponse(
    IReadOnlyList<AlcadaConferenteResponse> Avaliacoes, string Destino, Guid? ConferenteId, string? Motivo);
