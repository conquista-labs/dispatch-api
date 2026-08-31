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

        grupo.MapPut("/{id:guid}/perfil", async (
                Guid id,
                EditarPerfilConferenteRequest request,
                EditarPerfilConferente casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, request.Nome, request.Email, cancellationToken);
                return resultado switch
                {
                    ResultadoEditarPerfilConferente.Sucesso => Results.NoContent(),
                    ResultadoEditarPerfilConferente.NaoEncontrado => Results.NotFound(),
                    ResultadoEditarPerfilConferente.EmailJaCadastrado => Results.Conflict(new { motivo = "e-mail já cadastrado" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("EditarPerfilConferente")
            .WithSummary("Edita nome e e-mail de um conferente (RF-25).")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
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

        // A Distribuidora vendo a fila de um conferente específico, em leitura (protótipo
        // aprovado: "Minha fila" também aparece no menu de quem é gestão, com um jeito de
        // trocar de conferente). Reaproveita ObterMinhaFila/ObterConcluidosHoje tal qual —
        // os dois já recebem um Conferente qualquer, nunca dependeram de "quem está logado";
        // só o /minha-fila (MinhaFilaEndpoints) resolve isso do token, porque lá é sempre "eu
        // mesmo". Aqui o id vem da URL de propósito — é a Distribuidora escolhendo de fora.
        grupo.MapGet("/{id:guid}/fila", async (
                Guid id,
                ObterMinhaFila casoDeUso,
                IConferenteRepository conferentes,
                IRelogio relogio,
                CancellationToken cancellationToken) =>
            {
                var conferente = await conferentes.ObterPorIdAsync(id, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var fila = await casoDeUso.ExecutarAsync(conferente, cancellationToken);
                var agora = relogio.Agora;
                return Results.Ok(new MinhaFilaResponse(
                    fila.PoolDisponivel.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList(),
                    fila.Atribuidos.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList(),
                    fila.EmConferencia.Select(p => MinhaFilaEndpoints.ParaResumo(p, agora)).ToList()));
            })
            .WithName("ObterFilaDoConferente")
            .WithSummary("Mesma leitura de Minha fila (RF-19), só que de um conferente específico — pra Distribuidora acompanhar, nunca agir.")
            .Produces<MinhaFilaResponse>()
            .Produces(StatusCodes.Status404NotFound);

        grupo.MapGet("/{id:guid}/concluidos-hoje", async (
                Guid id,
                ObterConcluidosHoje casoDeUso,
                IConferenteRepository conferentes,
                IPedidoReaberturaRepository pedidos,
                CancellationToken cancellationToken) =>
            {
                var conferente = await conferentes.ObterPorIdAsync(id, cancellationToken);
                if (conferente is null)
                {
                    return Results.NotFound(new { motivo = "conferente não encontrado" });
                }

                var concluidos = await casoDeUso.ExecutarAsync(conferente, cancellationToken);
                // Guid? explícito no seletor de valor — ver comentário equivalente em
                // MinhaFilaEndpoints (GetValueOrDefault de Dictionary<Guid,Guid> devolveria
                // Guid.Empty, não null, pra quem não tem pedido pendente).
                var pendentesPorProtocolo = (await pedidos.ObterPendentesPorProtocolosAsync(
                        concluidos.Select(p => p.Id).ToList(), cancellationToken))
                    .ToDictionary(p => p.ProtocoloId, Guid? (p) => p.Id);
                return Results.Ok(concluidos
                    .Select(p => MinhaFilaEndpoints.ParaResumoConcluido(p, pendentesPorProtocolo.GetValueOrDefault(p.Id)))
                    .ToList());
            })
            .WithName("ObterConcluidosHojeDoConferente")
            .WithSummary("Concluídos hoje de um conferente específico, pra Distribuidora (RF-24, leitura).")
            .Produces<IReadOnlyList<ProtocoloConcluidoResumo>>()
            .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record CadastrarConferenteRequest(string Nome, string Email, string Senha, Nivel Nivel, double JornadaHoras);

public sealed record CadastrarConferenteResponse(Guid ConferenteId);

public sealed record EditarNivelEJornadaRequest(Nivel Nivel, double JornadaHoras);

public sealed record EditarPerfilConferenteRequest(string Nome, string Email);

public sealed record MarcarPresencaRequest(bool Presente);
