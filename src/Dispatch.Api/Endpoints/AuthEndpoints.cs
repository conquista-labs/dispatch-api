using System.Security.Claims;
using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login", async (
                HttpContext httpContext,
                LoginRequest request,
                Autenticar autenticar,
                CancellationToken cancellationToken) =>
            {
                var resultado = await autenticar.ExecutarAsync(request.Email, request.Senha, httpContext.ObterOrigem(), cancellationToken);

                return resultado switch
                {
                    ResultadoAutenticacao.Autenticado autenticado => Results.Ok(new LoginResponse(
                        autenticado.Token,
                        new UsuarioResponse(autenticado.UsuarioId, autenticado.Nome, autenticado.Email, autenticado.Papeis, autenticado.TrocarSenha))),
                    _ => Results.Unauthorized()
                };
            })
            .WithName("Login")
            .WithSummary("Autentica por e-mail e senha (RF-01/RF-02) e devolve um token JWT + os dados do usuário.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces<LoginResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();

        app.MapGet("/auth/me", async (
                ClaimsPrincipal principal,
                ObterUsuarioAtual casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var usuarioId = principal.ObterUsuarioId();
                var usuario = await casoDeUso.ExecutarAsync(usuarioId, cancellationToken);
                return usuario is null
                    ? Results.NotFound()
                    : Results.Ok(new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Papeis, usuario.TrocarSenha));
            })
            .WithName("ObterUsuarioAtual")
            .WithSummary("Devolve quem está logado, a partir do token — o front usa isso pra reidratar a sessão no boot, sem decodificar o JWT.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces<UsuarioResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();

        // RF-45 / ADR-0040: troca de senha com a sessão aberta — é a única saída de uma conta com
        // senha inicial (o middleware de Program.cs barra o resto). Devolve um token novo sem a
        // claim de troca, porque a troca encerra as sessões anteriores.
        app.MapPost("/auth/trocar-senha", async (
                TrocarSenhaRequest request,
                ClaimsPrincipal principal,
                TrocarSenhaInicial casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(principal.ObterUsuarioId(), request.SenhaAtual, request.NovaSenha, cancellationToken);
                return resultado switch
                {
                    ResultadoTrocarSenhaInicial.Sucesso sucesso => Results.Ok(new TrocarSenhaResponse(sucesso.Token)),
                    ResultadoTrocarSenhaInicial.NaoEncontrado => Results.NotFound(new { motivo = "usuário não encontrado" }),
                    ResultadoTrocarSenhaInicial.SenhaAtualIncorreta => Results.BadRequest(
                        new { codigo = "senha_atual_incorreta", motivo = "a senha atual não confere" }),
                    ResultadoTrocarSenhaInicial.SenhaFraca => Results.BadRequest(
                        new { codigo = "senha_fraca", motivo = "a nova senha precisa ter 12+ caracteres e não começar com um prefixo óbvio" }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("TrocarSenha")
            .WithSummary("Troca a senha com a sessão aberta (obrigatória no primeiro acesso de conta criada por outra pessoa, RF-45) e devolve um token novo.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces<TrocarSenhaResponse>()
            .Produces(StatusCodes.Status400BadRequest)
            .RequireAuthorization();
    }
}

public sealed record LoginRequest(string Email, string Senha);

public sealed record LoginResponse(string Token, UsuarioResponse Usuario);

// Papeis (não Papel) — pode ter mais de um (distribuidora que também confere, ver
// PapeisEfetivos no back). O front reflete isso como Usuario.papeis: Papel[].
// TrocarSenha (ADR-0040): conta com senha inicial — o front manda pra tela de troca.
public sealed record UsuarioResponse(Guid Id, string Nome, string Email, IReadOnlyList<Papel> Papeis, bool TrocarSenha = false);

public sealed record TrocarSenhaRequest(string SenhaAtual, string NovaSenha);

public sealed record TrocarSenhaResponse(string Token);
