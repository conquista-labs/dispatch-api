using Dispatch.Api.OpenApi;
using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Dispatch.Api.Endpoints;

public static class ImportacaoEndpoints
{
    // O relatório real tem poucas dezenas de KB. 5 MB deixa folga enorme e ainda impede que um arquivo
    // errado (um PDF escaneado, um backup) seja copiado pra memória do servidor (free tier).
    internal const long TamanhoMaximoDoRelatorio = 5 * 1024 * 1024;

    public static void MapImportacaoEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/protocolos/importar")
            .RequireAuthorization(policy => policy.RequireRole(nameof(Papel.Distribuidora)))
            .WithTags(OpenApiTags.Importacao);

        grupo.MapPost("/pre-visualizar", async (
                ImportarLoteRequest request,
                ImportarLote casoDeUso,
                ObterConfiguracao obterConfiguracao,
                CancellationToken cancellationToken) =>
            {
                // RF-08/RF-14: faixas do semáforo vêm da tabela `config` (seção 8).
                var config = await obterConfiguracao.ExecutarAsync(cancellationToken);
                var resumo = await casoDeUso.PreVisualizarAsync(
                    ParaLinhas(request), request.Etapa, request.LinhaDeCorte, config.FaixaAtencao, config.FaixaUrgente, cancellationToken);
                return Results.Ok(resumo);
            })
            .WithName("PreVisualizarImportacao")
            .WithSummary("Roda a distribuição do lote inteiro sem gravar nada (RF-10/RF-11) — pra revisar antes de confirmar.")
            .Produces<ResumoImportacao>();

        grupo.MapPost("/confirmar", async (
                ImportarLoteRequest request,
                ImportarLote casoDeUso,
                CancellationToken cancellationToken) =>
            {
                var resumo = await casoDeUso.ConfirmarAsync(
                    ParaLinhas(request), request.Etapa, request.LinhaDeCorte, cancellationToken);
                return Results.Ok(resumo);
            })
            .WithName("ConfirmarImportacao")
            .WithSummary("Roda a mesma distribuição e grava o resultado (RF-12).")
            .Produces<ResumoImportacao>();

        // Upload do relatório do cartório (ADR-0045): converte o arquivo nas mesmas linhas que a tela
        // colaria em /pre-visualizar. Não grava nada — a tela segue o fluxo de sempre com o resultado.
        //
        // multipart/form-data (e não JSON com o arquivo em base64): é o jeito nativo do navegador enviar
        // arquivo (FormData), sem inflar ~33% o tamanho nem montar string gigante no front. O ASP.NET
        // entrega a parte do arquivo como IFormFile, já bufferizada, quando o parâmetro tem esse tipo.
        grupo.MapPost("/converter", async (
                IFormFile? arquivo,
                ConverterRelatorio casoDeUso,
                CancellationToken cancellationToken) =>
            {
                // IFormFile? (anulável) pra responder com o nosso corpo de erro; não anulável, o
                // framework devolveria um 400 sem motivo quando o campo não vem.
                if (arquivo is null)
                {
                    return Results.BadRequest(new ErroConversaoResponse("arquivo_ausente", "envie o relatório no campo 'arquivo'"));
                }
                if (arquivo.Length > TamanhoMaximoDoRelatorio)
                {
                    return Results.Json(
                        new ErroConversaoResponse("arquivo_grande_demais", "o arquivo passa de 5 MB — o relatório do cartório tem poucos KB; confira se é o arquivo certo"),
                        statusCode: StatusCodes.Status413PayloadTooLarge);
                }

                await using var conteudo = arquivo.OpenReadStream();
                var resultado = await casoDeUso.ExecutarAsync(conteudo, cancellationToken);
                return resultado switch
                {
                    ResultadoConversaoRelatorio.Convertido c => Results.Ok(new RelatorioConvertidoResponse(
                        c.Conector,
                        c.Etapa,
                        c.Linhas.Select(l => new LinhaConvertidaResponse(l.Protocolo, l.TipoAto, l.Escrevente, l.DataHoraAndamento)).ToList(),
                        c.TotalDeclarado,
                        c.TotalLido)),
                    ResultadoConversaoRelatorio.FormatoNaoReconhecido => Results.BadRequest(new ErroConversaoResponse(
                        "formato_nao_reconhecido",
                        "o arquivo não é um Relatório de Andamentos dos Protocolos em .xls (Excel 97-2003) — confira se exportou o relatório certo")),
                    ResultadoConversaoRelatorio.AndamentoNaoEhConferencia a => Results.BadRequest(new ErroConversaoResponse(
                        "formato_nao_reconhecido",
                        $"o relatório é do andamento \"{a.Andamento}\" — o Dispatch importa só pré ou pós-conferência")),
                    ResultadoConversaoRelatorio.EtapasMisturadas => Results.BadRequest(new ErroConversaoResponse(
                        "etapas_misturadas",
                        "o relatório traz pré e pós-conferência juntas — cada lote é de uma etapa só; exporte uma de cada vez")),
                    ResultadoConversaoRelatorio.TotaisNaoConferem t => Results.BadRequest(new ErroConversaoResponse(
                        "totais_nao_conferem",
                        $"o relatório declara {t.TotalDeclarado} protocolo(s), mas foram lidos {t.TotalLido} — o layout do relatório pode ter mudado; nada foi importado")),
                    ResultadoConversaoRelatorio.RelatorioVazio => Results.BadRequest(new ErroConversaoResponse(
                        "relatorio_vazio",
                        "o relatório não tem nenhum protocolo")),
                    _ => throw new InvalidOperationException($"Resultado não mapeado: {resultado.GetType().Name}"),
                };
            })
            // Antiforgery (proteção contra CSRF) vem ligado por padrão em endpoint minimal API que lê
            // formulário/IFormFile, e sem o middleware UseAntiforgery a chamada falha. CSRF é o ataque
            // em que o navegador anexa sozinho um COOKIE de sessão a um post forjado por outro site;
            // aqui a autenticação é o token Bearer no header Authorization, que o navegador nunca
            // anexa sozinho — não há o que proteger, então desliga só nesta rota.
            .DisableAntiforgery()
            // Teto do corpo inteiro no Kestrel (acima dos 5 MB checados acima, pra sobrar espaço pro
            // envelope multipart): upload maior é cortado antes de ser bufferizado, com 413 sem corpo.
            .WithMetadata(new RequestSizeLimitAttribute(2 * TamanhoMaximoDoRelatorio))
            .WithName("ConverterRelatorio")
            .WithSummary("RF-05/RF-06 — converte o .xls do cartório nas linhas da importação (etapa + linhas + totais conferidos). Não grava nada.")
            .Produces<RelatorioConvertidoResponse>()
            .Produces<ErroConversaoResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErroConversaoResponse>(StatusCodes.Status413PayloadTooLarge);
    }

    private static List<LinhaImportacao> ParaLinhas(ImportarLoteRequest request) =>
        request.Linhas
            .Select(l => new LinhaImportacao(l.Protocolo, l.TipoAto, l.Escrevente, l.DataHoraAndamento))
            .ToList();
}

public sealed record ImportarLoteRequest(Etapa Etapa, DateTimeOffset LinhaDeCorte, IReadOnlyList<LinhaImportacaoRequest> Linhas);

public sealed record LinhaImportacaoRequest(string Protocolo, string TipoAto, string Escrevente, DateTimeOffset DataHoraAndamento);

public sealed record RelatorioConvertidoResponse(
    string Conector, Etapa Etapa, IReadOnlyList<LinhaConvertidaResponse> Linhas, int TotalDeclarado, int TotalLido);

// Mesmos campos de LinhaImportacaoRequest: a tela manda as linhas convertidas direto pra /pre-visualizar.
public sealed record LinhaConvertidaResponse(string Protocolo, string TipoAto, string Escrevente, DateTimeOffset DataHoraAndamento);

public sealed record ErroConversaoResponse(string Codigo, string Motivo);
