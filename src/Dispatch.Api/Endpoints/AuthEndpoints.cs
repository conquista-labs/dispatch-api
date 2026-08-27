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
                LoginRequest request,
                Autenticar autenticar,
                CancellationToken cancellationToken) =>
            {
                var resultado = await autenticar.ExecutarAsync(request.Email, request.Senha, cancellationToken);

                return resultado switch
                {
                    ResultadoAutenticacao.Autenticado autenticado => Results.Ok(new LoginResponse(
                        autenticado.Token,
                        new UsuarioResponse(autenticado.UsuarioId, autenticado.Nome, autenticado.Email, autenticado.Papel))),
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
                var usuarioId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var usuario = await casoDeUso.ExecutarAsync(usuarioId, cancellationToken);
                return usuario is null
                    ? Results.NotFound()
                    : Results.Ok(new UsuarioResponse(usuario.Id, usuario.Nome, usuario.Email, usuario.Papel));
            })
            .WithName("ObterUsuarioAtual")
            .WithSummary("Devolve quem está logado, a partir do token — o front usa isso pra reidratar a sessão no boot, sem decodificar o JWT.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces<UsuarioResponse>()
            .Produces(StatusCodes.Status404NotFound)
            .RequireAuthorization();
    }
}

public sealed record LoginRequest(string Email, string Senha);

public sealed record LoginResponse(string Token, UsuarioResponse Usuario);

public sealed record UsuarioResponse(Guid Id, string Nome, string Email, Papel Papel);
