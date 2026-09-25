using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;

namespace Dispatch.Api.Tests;

// RF-24k ("↻ 2ª conferência"), ponta a ponta pelo fluxo real: importar → pegar → iniciar →
// reprovar → reimportar o mesmo Número com andamento posterior. O que só a integração prova: a
// projeção de ObterRegistrosPorNumerosAsync traduz pro SQL, e o número chega igual nas três
// leituras que o front usa (Minha fila, Distribuição, detalhe). ADR-0038.
[Collection(IntegracaoCollection.Nome)]
public sealed class NumeroDaConferenciaIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private const string Numero = "RODADA-E2E-1";
    private static readonly DateTimeOffset PrimeiroAndamento = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SegundoAndamento = new(2026, 9, 3, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProtocoloQueVoltaDepoisDeReprovado_EhASegundaConferenciaNasTresLeituras()
    {
        await ImportarAsync(PrimeiroAndamento, linhaDeCorte: PrimeiroAndamento.AddDays(-1));
        var primeiraId = await ObterIdPorNumeroAsync(StatusProtocolo.Pool);

        var conferente = await AutenticarComoAsync(Papel.Conferente);
        await EsperarSucessoAsync(conferente.PostAsync($"/minha-fila/{primeiraId}/pegar", null));
        await EsperarSucessoAsync(conferente.PostAsync($"/minha-fila/{primeiraId}/iniciar", null));
        await EsperarSucessoAsync(conferente.PostAsJsonAsync($"/minha-fila/{primeiraId}/concluir", new { aprovado = false }));

        await ImportarAsync(SegundoAndamento, linhaDeCorte: PrimeiroAndamento);
        // Continuidade (ADR-0022): volta direto pra quem fez a primeira conferência.
        var segundaId = await ObterIdPorNumeroAsync(StatusProtocolo.Atribuido);

        conferente = await AutenticarComoAsync(Papel.Conferente);
        var fila = await conferente.GetFromJsonAsync<JsonElement>("/minha-fila");
        var naFila = fila.GetProperty("atribuidos").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == segundaId);
        Assert.Equal(2, naFila.GetProperty("numeroDaConferencia").GetInt32());

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var visao = await distribuidora.GetFromJsonAsync<JsonElement>("/protocolos/distribuicao");
        Assert.Equal(2, NumeroNaVisao(visao, "atribuidos", segundaId));
        Assert.Equal(1, NumeroNaVisao(visao, "concluidos", primeiraId));

        var detalhe = await distribuidora.GetFromJsonAsync<JsonElement>($"/protocolos/{segundaId}/detalhe");
        Assert.Equal(2, detalhe.GetProperty("numeroDaConferencia").GetInt32());
        var anterior = detalhe.GetProperty("historicoConferencias").EnumerateArray().Single();
        Assert.Equal(primeiraId, anterior.GetProperty("protocoloId").GetGuid());
        Assert.Equal(1, anterior.GetProperty("numeroDaConferencia").GetInt32());
    }

    private async Task ImportarAsync(DateTimeOffset andamento, DateTimeOffset linhaDeCorte)
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var resposta = await distribuidora.PostAsJsonAsync("/protocolos/importar/confirmar", new
        {
            etapa = nameof(Etapa.PosConferencia),
            linhaDeCorte,
            linhas = new[] { new { protocolo = Numero, tipoAto = "Ato Rodada E2E", escrevente = "Escrevente Rodada E2E", dataHoraAndamento = andamento } },
        });
        Assert.True(resposta.IsSuccessStatusCode, $"status={resposta.StatusCode} corpo={await resposta.Content.ReadAsStringAsync()}");
    }

    private async Task<Guid> ObterIdPorNumeroAsync(StatusProtocolo statusEsperado)
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var visao = await distribuidora.GetFromJsonAsync<JsonElement>("/protocolos/distribuicao");
        var bucket = statusEsperado == StatusProtocolo.Pool ? "pool" : "atribuidos";
        return visao.GetProperty(bucket).EnumerateArray()
            .Single(p => p.GetProperty("numero").GetString() == Numero)
            .GetProperty("id").GetGuid();
    }

    private static int NumeroNaVisao(JsonElement visao, string bucket, Guid protocoloId) =>
        visao.GetProperty(bucket).EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == protocoloId)
            .GetProperty("numeroDaConferencia").GetInt32();

    private static async Task EsperarSucessoAsync(Task<HttpResponseMessage> chamada)
    {
        var resposta = await chamada;
        Assert.True(
            resposta.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK,
            $"status={resposta.StatusCode} corpo={await resposta.Content.ReadAsStringAsync()}");
    }
}
