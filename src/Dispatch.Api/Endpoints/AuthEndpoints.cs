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

        app.MapPost("/auth/totp/registrar", async (
                ClaimsPrincipal principal,
                RegistrarTotp registrar,
                CancellationToken cancellationToken) =>
            {
                var usuarioId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var resultado = await registrar.ExecutarAsync(usuarioId, cancellationToken);
                return resultado is null
                    ? Results.NotFound()
                    : Results.Ok(new RegistrarTotpResponse(resultado.ChaveBase32, resultado.UriOtpAuth));
            })
            .WithName("RegistrarTotp")
            .WithSummary("RF-01a-c: gera um segredo TOTP novo (pendente de confirmação) e devolve a chave Base32 + a URI otpauth:// pro QR.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces<RegistrarTotpResponse>()
            .RequireAuthorization();

        app.MapPost("/auth/totp/confirmar", async (
                ClaimsPrincipal principal,
                ConfirmarTotpRequest request,
                ConfirmarRegistroTotp confirmar,
                CancellationToken cancellationToken) =>
            {
                var usuarioId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var resultado = await confirmar.ExecutarAsync(usuarioId, request.Codigo, cancellationToken);
                return resultado switch
                {
                    ResultadoConfirmarTotp.Sucesso => Results.NoContent(),
                    ResultadoConfirmarTotp.SemRegistroPendente => Results.NotFound(),
                    _ => Results.BadRequest()
                };
            })
            .WithName("ConfirmarRegistroTotp")
            .WithSummary("RF-01d: confirma o código de 6 dígitos e ativa o autenticador.")
            .WithTags(OpenApiTags.Autenticacao)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest)
            .RequireAuthorization();

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

public sealed record LoginRequest(string Email, string Senha);

public sealed record LoginResponse(string Token, UsuarioResponse Usuario);

public sealed record UsuarioResponse(Guid Id, string Nome, string Email, Papel Papel);

public sealed record RegistrarTotpResponse(string ChaveBase32, string UriOtpAuth);

public sealed record ConfirmarTotpRequest(string Codigo);

public sealed record IniciarRecuperacaoRequest(string Email);

public sealed record ValidarCodigoRecuperacaoRequest(string Email, string Codigo);

public sealed record ValidarCodigoRecuperacaoResponse(string TokenRecuperacao);

public sealed record RedefinirSenhaRequest(string TokenRecuperacao, string NovaSenha);
