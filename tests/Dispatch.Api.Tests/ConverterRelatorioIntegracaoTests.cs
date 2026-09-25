using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dispatch.Api.Tests.Conectores;
using Dispatch.Domain;

namespace Dispatch.Api.Tests;

// POST /protocolos/importar/converter pelo pipeline real: multipart de verdade (binding de IFormFile,
// antiforgery desligado — sem o DisableAntiforgery a chamada falha em runtime, coisa que teste de
// unidade não pega), o conector resolvido pelo DI e o JSON como o front recebe.
[Collection(IntegracaoCollection.Nome)]
public sealed class ConverterRelatorioIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private const string Rota = "/protocolos/importar/converter";

    [Fact]
    public async Task RelatorioXls_DevolveEtapaLinhasETotais()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);

        var resposta = await cliente.PostAsync(Rota, Multipart(File.ReadAllBytes(
            ConversorRelatorioDeAndamentosXlsTests.CaminhoDoFixture("relatorio-pre-conferencia.xls"))));

        var corpo = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, corpo);
        using var json = JsonDocument.Parse(corpo);
        var raiz = json.RootElement;
        Assert.Equal("Relatório de Andamentos dos Protocolos", raiz.GetProperty("conector").GetString());
        Assert.Equal("PreConferencia", raiz.GetProperty("etapa").GetString());
        Assert.Equal(5, raiz.GetProperty("totalDeclarado").GetInt32());
        Assert.Equal(5, raiz.GetProperty("totalLido").GetInt32());

        var primeira = raiz.GetProperty("linhas")[0];
        Assert.Equal("900101", primeira.GetProperty("protocolo").GetString());
        Assert.Equal("VENDA E COMPRA", primeira.GetProperty("tipoAto").GetString());
        Assert.Equal("ANA PAULA FICTICIA", primeira.GetProperty("escrevente").GetString());
        // Offset de Brasília preservado no JSON — é o horário que a pessoa lê no relatório.
        Assert.Equal("2026-09-22T17:34:05-03:00", primeira.GetProperty("dataHoraAndamento").GetString());
        Assert.Equal(5, raiz.GetProperty("linhas").GetArrayLength());

        // Nada gravado: converter só lê o arquivo.
        await NoBancoAsync(db =>
        {
            Assert.Empty(db.Protocolos);
            Assert.Empty(db.Escreventes);
            return Task.CompletedTask;
        });
    }

    // As linhas convertidas entram direto no fluxo de sempre, sem retoque do front.
    [Fact]
    public async Task LinhasConvertidas_PassamNaPreVisualizacao()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);
        var convertido = await (await cliente.PostAsync(Rota, Multipart(File.ReadAllBytes(
                ConversorRelatorioDeAndamentosXlsTests.CaminhoDoFixture("relatorio-pre-conferencia.xls")))))
            .Content.ReadFromJsonAsync<JsonElement>();

        var previa = await cliente.PostAsJsonAsync("/protocolos/importar/pre-visualizar", new
        {
            etapa = convertido.GetProperty("etapa").GetString(),
            linhaDeCorte = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            linhas = convertido.GetProperty("linhas"),
        });

        Assert.True(previa.IsSuccessStatusCode, await previa.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ArquivoQueNaoEhXls_400ComCodigo()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);

        var resposta = await cliente.PostAsync(Rota, Multipart(Encoding.UTF8.GetBytes("protocolo,tipoAto\n900001,VENDA\n"), "relatorio.csv"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("formato_nao_reconhecido", erro.GetProperty("codigo").GetString());
        Assert.False(string.IsNullOrWhiteSpace(erro.GetProperty("motivo").GetString()));
    }

    [Fact]
    public async Task TotaisQueNaoConferem_400ComCodigo()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);

        var resposta = await cliente.PostAsync(Rota, Multipart(File.ReadAllBytes(
            ConversorRelatorioDeAndamentosXlsTests.CaminhoDoFixture("relatorio-totais-nao-conferem.xls"))));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("totais_nao_conferem", erro.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task SemOCampoArquivo_400ArquivoAusente()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);
        using var corpo = new MultipartFormDataContent { { new StringContent("x"), "outroCampo" } };

        var resposta = await cliente.PostAsync(Rota, corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("arquivo_ausente", erro.GetProperty("codigo").GetString());
    }

    [Fact]
    public async Task AcimaDe5MB_413()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);

        var resposta = await cliente.PostAsync(Rota, Multipart(new byte[5 * 1024 * 1024 + 1]));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, resposta.StatusCode);
        var erro = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("arquivo_grande_demais", erro.GetProperty("codigo").GetString());
    }

    // Mesmo papel das outras rotas de importação (grupo Distribuidora).
    [Fact]
    public async Task Conferente_403()
    {
        var cliente = await AutenticarComoAsync(Papel.Conferente);

        var resposta = await cliente.PostAsync(Rota, Multipart(File.ReadAllBytes(
            ConversorRelatorioDeAndamentosXlsTests.CaminhoDoFixture("relatorio-pre-conferencia.xls"))));

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    private static MultipartFormDataContent Multipart(byte[] bytes, string nomeDoArquivo = "relatorio.xls")
    {
        var arquivo = new ByteArrayContent(bytes);
        arquivo.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.ms-excel");
        return new MultipartFormDataContent { { arquivo, "arquivo", nomeDoArquivo } };
    }
}
