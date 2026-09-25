using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Api.Endpoints;

// Seção 8 do documento de requisitos — tabela de configuração do sistema, editável sem
// redeploy (antes vivia hardcoded em vários casos de uso/endpoints, ver
// docs/decisions/0023-tabela-config-de-linha-unica.md).
// Sem tela própria no front ainda — editável via GET/PUT direto (curl/Swagger), Distribuidora.
public static class ConfiguracaoEndpoints
{
    public static void MapConfiguracaoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/config")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.Sistema);

        grupo.MapGet("/", async (ObterConfiguracao casoDeUso, CancellationToken cancellationToken) =>
                Results.Ok(ParaResponse(await casoDeUso.ExecutarAsync(cancellationToken))))
            .WithName("ObterConfiguracao")
            .WithSummary("Os 12 valores de configuração do sistema (faixas do semáforo, limites, janelas, limiares de aprendizado).")
            .Produces<ConfiguracaoResponse>();

        grupo.MapPut("/", async (ConfiguracaoRequest request, AtualizarConfiguracao casoDeUso, CancellationToken cancellationToken) =>
            {
                var resultado = await casoDeUso.ExecutarAsync(
                    TimeSpan.FromMinutes(request.FaixaAtencaoMinutos), TimeSpan.FromMinutes(request.FaixaUrgenteMinutos),
                    request.LimiteDeAtosSimultaneos, TimeSpan.FromMinutes(request.JanelaDeCorrecaoMinutos),
                    request.DiasDeMemoriaDescarte, request.TempoMedioPorAtoMinutos,
                    request.LimiarTipoDesconhecido, request.LimiarPrazoIrrealCasos, request.LimiarPrazoIrrealEstouro,
                    request.LimiarEscreventeOrfao, request.LimiarRiscoQualidadeCasos, request.LimiarRiscoQualidadeReprovacao,
                    cancellationToken);
                return resultado switch
                {
                    ResultadoAtualizarConfiguracao.Sucesso => Results.NoContent(),
                    ResultadoAtualizarConfiguracao.ValorInvalido invalido => Results.BadRequest(new { motivo = invalido.Motivo }),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}")
                };
            })
            .WithName("AtualizarConfiguracao")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Administrador)))
            .WithSummary("Substitui os 12 valores juntos — sem edição parcial, mesmo padrão de PUT já usado em Equipe/TipoAto.")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);
    }

    // Minutos, não TimeSpan cru — mais fácil de editar via curl/Swagger do que o formato
    // "hh:mm:ss" que o System.Text.Json usa pra TimeSpan por padrão.
    private static ConfiguracaoResponse ParaResponse(Configuracao c) => new(
        (int)c.FaixaAtencao.TotalMinutes, (int)c.FaixaUrgente.TotalMinutes, c.LimiteDeAtosSimultaneos,
        (int)c.JanelaDeCorrecao.TotalMinutes, c.DiasDeMemoriaDescarte, c.TempoMedioPorAtoMinutos,
        c.LimiarTipoDesconhecido, c.LimiarPrazoIrrealCasos, c.LimiarPrazoIrrealEstouro,
        c.LimiarEscreventeOrfao, c.LimiarRiscoQualidadeCasos, c.LimiarRiscoQualidadeReprovacao);
}

public sealed record ConfiguracaoResponse(
    int FaixaAtencaoMinutos, int FaixaUrgenteMinutos, int LimiteDeAtosSimultaneos, int JanelaDeCorrecaoMinutos,
    int DiasDeMemoriaDescarte, double TempoMedioPorAtoMinutos, int LimiarTipoDesconhecido, int LimiarPrazoIrrealCasos,
    double LimiarPrazoIrrealEstouro, int LimiarEscreventeOrfao, int LimiarRiscoQualidadeCasos, double LimiarRiscoQualidadeReprovacao);

public sealed record ConfiguracaoRequest(
    int FaixaAtencaoMinutos, int FaixaUrgenteMinutos, int LimiteDeAtosSimultaneos, int JanelaDeCorrecaoMinutos,
    int DiasDeMemoriaDescarte, double TempoMedioPorAtoMinutos, int LimiarTipoDesconhecido, int LimiarPrazoIrrealCasos,
    double LimiarPrazoIrrealEstouro, int LimiarEscreventeOrfao, int LimiarRiscoQualidadeCasos, double LimiarRiscoQualidadeReprovacao);
