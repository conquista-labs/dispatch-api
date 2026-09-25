using System.Security.Claims;
using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

// Contas de gestão (RF-44 a RF-47) — só Administrador (ADR-0039). Conferentes continuam em
// /conferentes. Reativar conta e trocar papel ficam fora (decisão consciente; o protótipo também
// não tem).
public static class ContaEndpoints
{
    public static void MapContaEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/contas")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)))
            .WithTags(OpenApiTags.Contas);

        grupo.MapGet("/", async (ListarContas casoDeUso, ClaimsPrincipal usuario, CancellationToken cancellationToken) =>
                Results.Ok(await casoDeUso.ExecutarAsync(usuario.ObterUsuarioId(), cancellationToken)))
            .WithName("ListarContas")
            .WithSummary("RF-44 — contas de administrador e distribuidora, com \"também confere\", situação e \"você\".")
            .Produces<IReadOnlyList<ContaResumo>>();

        grupo.MapPost("/", async (CriarContaRequest request, CriarConta casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(request.Nome, request.Email, request.SenhaInicial, request.Papel, cancellationToken);
                return resultado switch
                {
                    ResultadoCriarConta.Sucesso sucesso => Results.Created($"/contas/{sucesso.UsuarioId}", new CriarContaResponse(sucesso.UsuarioId)),
                    ResultadoCriarConta.PapelInvalido => Results.BadRequest(new { motivo = "o papel precisa ser Distribuidora ou Administrador" }),
                    ResultadoCriarConta.DadosInvalidos => Results.BadRequest(new { motivo = "preencha o nome e um e-mail válido" }),
                    ResultadoCriarConta.SenhaInicialCurta => Results.BadRequest(
                        new { motivo = $"a senha inicial precisa ter pelo menos {RegrasDeSenha.ComprimentoMinimoSenhaInicial} caracteres" }),
                    ResultadoCriarConta.EmailJaCadastrado => Results.Conflict(new { motivo = "já existe uma conta com esse e-mail" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("CriarConta")
            .WithSummary("RF-45 — cria conta de Distribuidora ou Administrador; a pessoa troca a senha no primeiro acesso.")
            .Produces<CriarContaResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        grupo.MapPost("/{id:guid}/desativar", async (Guid id, DesativarConta casoDeUso, ClaimsPrincipal usuario, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(id, usuario.ObterUsuarioId(), cancellationToken);
                // `codigo` é o que o front usa pra escolher a mensagem de cada trava (RF-47);
                // `motivo` é o texto legível pra quem chama pela API.
                return resultado switch
                {
                    ResultadoDesativarConta.Sucesso => Results.NoContent(),
                    ResultadoDesativarConta.NaoEncontrada => Results.NotFound(new { motivo = "conta não encontrada" }),
                    ResultadoDesativarConta.JaInativa => Results.Conflict(new { codigo = "ja_inativa", motivo = "a conta já está inativa" }),
                    ResultadoDesativarConta.ContaDeConferente => Results.Conflict(
                        new { codigo = "conta_de_conferente", motivo = "conta de conferente se remove em Conferentes" }),
                    ResultadoDesativarConta.PropriaConta => Results.Conflict(
                        new { codigo = "propria_conta", motivo = "ninguém desativa a própria conta" }),
                    ResultadoDesativarConta.UltimoAdministrador => Results.Conflict(
                        new { codigo = "ultimo_administrador", motivo = "sempre fica pelo menos um administrador ativo" }),
                    ResultadoDesativarConta.PropriaEUltimoAdministrador => Results.Conflict(
                        new { codigo = "propria_e_ultimo_administrador", motivo = "é a sua conta e a do último administrador ativo" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("DesativarConta")
            .WithSummary("RF-46/RF-47 — tira o acesso e preserva histórico; se a conta também confere, sai da escala e os atribuídos voltam ao pool.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }
}

public sealed record CriarContaRequest(string Nome, string Email, string SenhaInicial, Papel Papel);

public sealed record CriarContaResponse(Guid UsuarioId);
