using System.Security.Claims;
using Dispatch.Api.OpenApi;
using Dispatch.Application;

namespace Dispatch.Api.Endpoints;

// Extraído de AuthEndpoints.cs (auditoria de qualidade) — registro do autenticador TOTP é um
// fluxo separado do login normal (ver dispatch-api/CLAUDE.md, seção "TOTP e recuperação de
// senha"), só serve de prova de identidade na recuperação de senha.
public static class TotpEndpoints
{
    public static void MapTotpEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/totp/registrar", async (
                ClaimsPrincipal principal,
                RegistrarTotp registrar,
                CancellationToken cancellationToken) =>
            {
                var usuarioId = principal.ObterUsuarioId();
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
                var usuarioId = principal.ObterUsuarioId();
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
    }
}

public sealed record RegistrarTotpResponse(string ChaveBase32, string UriOtpAuth);

public sealed record ConfirmarTotpRequest(string Codigo);
