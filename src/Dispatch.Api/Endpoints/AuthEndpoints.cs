using Dispatch.Api.OpenApi;
using Dispatch.Application;

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
                    ResultadoAutenticacao.Autenticado autenticado => Results.Ok(new LoginResponse(autenticado.Token)),
                    _ => Results.Unauthorized()
                };
            })
            .WithName("Login")
            .WithSummary("Autentica por e-mail e senha (RF-01/RF-02) e devolve um token JWT.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces<LoginResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();
    }
}

public sealed record LoginRequest(string Email, string Senha);

public sealed record LoginResponse(string Token);
