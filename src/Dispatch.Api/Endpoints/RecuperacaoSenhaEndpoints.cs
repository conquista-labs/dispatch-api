using Dispatch.Api.OpenApi;
using Dispatch.Application;

namespace Dispatch.Api.Endpoints;

// Extraído de AuthEndpoints.cs (auditoria de qualidade) — as 3 etapas de RF-01g, sempre
// anônimas (o usuário ainda não conseguiu logar, é por isso que está recuperando a senha).
public static class RecuperacaoSenhaEndpoints
{
    public static void MapRecuperacaoSenhaEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/recuperar/iniciar", async (
                IniciarRecuperacaoRequest request,
                IniciarRecuperacaoSenha iniciar,
                CancellationToken cancellationToken) =>
            {
                await iniciar.ExecutarAsync(request.Email, cancellationToken);
                return Results.Ok();
            })
            .WithName("IniciarRecuperacaoSenha")
            .WithSummary("RF-01g etapa 1 / RF-01h: sempre responde 200, exista ou não o e-mail — nunca revela se a conta existe.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces(StatusCodes.Status200OK)
            .AllowAnonymous();

        app.MapPost("/auth/recuperar/validar-codigo", async (
                ValidarCodigoRecuperacaoRequest request,
                ValidarCodigoRecuperacao validar,
                CancellationToken cancellationToken) =>
            {
                var resultado = await validar.ExecutarAsync(request.Email, request.Codigo, cancellationToken);
                return resultado switch
                {
                    ResultadoValidarCodigoRecuperacao.TokenEmitido emitido => Results.Ok(new ValidarCodigoRecuperacaoResponse(emitido.Token)),
                    ResultadoValidarCodigoRecuperacao.Bloqueado bloqueado => Results.Json(
                        new { bloqueadoAte = bloqueado.BloqueadoAte }, statusCode: StatusCodes.Status423Locked),
                    _ => Results.Unauthorized()
                };
            })
            .WithName("ValidarCodigoRecuperacao")
            .WithSummary("RF-01g etapa 2 / RF-01i: valida o código do autenticador e devolve o token de recuperação (uso único, 10 minutos).")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces<ValidarCodigoRecuperacaoResponse>()
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status423Locked)
            .AllowAnonymous();

        app.MapPost("/auth/recuperar/redefinir-senha", async (
                RedefinirSenhaRequest request,
                RedefinirSenha redefinir,
                CancellationToken cancellationToken) =>
            {
                var resultado = await redefinir.ExecutarAsync(request.TokenRecuperacao, request.NovaSenha, cancellationToken);
                return resultado switch
                {
                    ResultadoRedefinirSenha.Sucesso => Results.NoContent(),
                    ResultadoRedefinirSenha.SenhaFraca => Results.BadRequest(),
                    _ => Results.Unauthorized()
                };
            })
            .WithName("RedefinirSenha")
            .WithSummary("RF-01g etapa 3 / RF-01j / RF-01k: troca a senha, encerra todas as sessões e devolve pro pool os atos em conferência.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .AllowAnonymous();
    }
}

public sealed record IniciarRecuperacaoRequest(string Email);

public sealed record ValidarCodigoRecuperacaoRequest(string Email, string Codigo);

public sealed record ValidarCodigoRecuperacaoResponse(string TokenRecuperacao);

public sealed record RedefinirSenhaRequest(string TokenRecuperacao, string NovaSenha);
