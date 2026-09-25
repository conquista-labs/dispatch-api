using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;

namespace Dispatch.Api.Tests;

// Dashboard v2 (RF-42b/42c/43) pelo pipeline real: o que fake não prova é a forma do JSON — os campos
// novos em camelCase, a granularidade como string, o dia da série como "yyyy-MM-dd", o nulo presente
// — e a busca de continuidade (RF-24k) contra o Postgres. Os números vêm de um fluxo de verdade:
// importar → pegar → iniciar → concluir.
[Collection(IntegracaoCollection.Nome)]
public sealed class DashboardIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    [Fact]
    public async Task Mes_TrazPeriodoKpisAnteriorSerieEAprovadoNaPrimeira_NasDuasVisoes()
    {
        // Dois protocolos de escrevente sem equipe (prazo D+1) com andamento de 10 dias atrás: já
        // estourados. A conferente seed aprova um e reprova o outro — 1ª conferência dos dois.
        var ids = await ImportarEstouradosAsync(2);
        var conferente = await AutenticarComoAsync(Papel.Conferente);
        await ConcluirAsync(conferente, ids[0], aprovado: true);
        await ConcluirAsync(conferente, ids[1], aprovado: false);
        var hoje = FusoHorario.DiaLocal(DateTimeOffset.UtcNow).ToString("yyyy-MM-dd");

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var gestao = await distribuidora.GetFromJsonAsync<JsonElement>("/dashboard?periodo=Mes");

        var inicio = gestao.GetProperty("periodoInicio").GetDateTimeOffset();
        Assert.Equal(1, inicio.ToOffset(FusoHorario.Brasilia).Day);
        Assert.Equal(TimeSpan.Zero, inicio.ToOffset(FusoHorario.Brasilia).TimeOfDay);
        Assert.True(gestao.GetProperty("periodoFim").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));

        var kpis = gestao.GetProperty("kpis");
        Assert.Equal(2, kpis.GetProperty("atosConferidos").GetInt32());
        Assert.Equal(0.5, kpis.GetProperty("percentualAprovado").GetDouble());
        Assert.Equal(0.5, kpis.GetProperty("percentualAprovadoNaPrimeira").GetDouble());
        Assert.Equal(0.0, kpis.GetProperty("percentualNoPrazo").GetDouble());

        var anterior = gestao.GetProperty("kpisAnterior");
        Assert.Equal(0, anterior.GetProperty("atosConferidos").GetInt32());
        Assert.Equal(JsonValueKind.Null, anterior.GetProperty("percentualAprovadoNaPrimeira").ValueKind);

        var serie = gestao.GetProperty("serie");
        Assert.Equal("Dia", serie.GetProperty("granularidade").GetString());
        var pontoDeHoje = serie.GetProperty("pontos").EnumerateArray().Single(p => p.GetProperty("inicio").GetString() == hoje);
        Assert.Equal(2, pontoDeHoje.GetProperty("conferidos").GetInt32());
        Assert.Equal(2, pontoDeHoje.GetProperty("estourados").GetInt32());
        Assert.False(pontoDeHoje.GetProperty("futuro").GetBoolean());

        var linha = gestao.GetProperty("desempenho").EnumerateArray().Single();
        Assert.Equal(0.5, linha.GetProperty("percentualAprovadoNaPrimeira").GetDouble());

        // Visão restrita: os mesmos campos, com os números dela e a média da casa.
        var restrita = await conferente.GetFromJsonAsync<JsonElement>("/dashboard?periodo=Mes");
        Assert.Equal(0.5, restrita.GetProperty("kpis").GetProperty("percentualAprovadoNaPrimeira").GetDouble());
        Assert.Equal(0, restrita.GetProperty("kpisAnterior").GetProperty("atosConferidos").GetInt32());
        Assert.Equal(2, restrita.GetProperty("serie").GetProperty("pontos").EnumerateArray().Sum(p => p.GetProperty("conferidos").GetInt32()));
        Assert.Equal(0.5, restrita.GetProperty("mediaDaCasa").GetProperty("percentualAprovadoNaPrimeira").GetDouble());
    }

    [Fact]
    public async Task Trimestre_SerieVemPorSemana()
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);

        var dashboard = await distribuidora.GetFromJsonAsync<JsonElement>("/dashboard?periodo=Trimestre");

        var serie = dashboard.GetProperty("serie");
        Assert.Equal("Semana", serie.GetProperty("granularidade").GetString());
        var pontos = serie.GetProperty("pontos").EnumerateArray().ToList();
        Assert.InRange(pontos.Count, 13, 14);
        Assert.All(pontos, p => Assert.Equal(DayOfWeek.Monday, DateOnly.Parse(p.GetProperty("inicio").GetString()!).DayOfWeek));
        Assert.Equal(JsonValueKind.Null, dashboard.GetProperty("kpis").GetProperty("percentualAprovadoNaPrimeira").ValueKind);
    }

    private async Task<Guid[]> ImportarEstouradosAsync(int quantidade)
    {
        var andamento = DateTimeOffset.UtcNow.AddDays(-10);
        var numeros = Enumerable.Range(1, quantidade).Select(i => $"DASH-E2E-{i}").ToArray();
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var resposta = await distribuidora.PostAsJsonAsync("/protocolos/importar/confirmar", new
        {
            etapa = nameof(Etapa.PosConferencia),
            linhaDeCorte = andamento.AddDays(-1),
            linhas = numeros.Select(n => new
            {
                protocolo = n,
                tipoAto = "Ato Dashboard E2E",
                escrevente = "Escrevente Dashboard E2E",
                dataHoraAndamento = andamento,
            }).ToArray(),
        });
        Assert.True(resposta.IsSuccessStatusCode, $"status={resposta.StatusCode} corpo={await resposta.Content.ReadAsStringAsync()}");

        var visao = await distribuidora.GetFromJsonAsync<JsonElement>("/protocolos/distribuicao");
        var pool = visao.GetProperty("pool").EnumerateArray().ToList();
        return numeros
            .Select(n => pool.Single(p => p.GetProperty("numero").GetString() == n).GetProperty("id").GetGuid())
            .ToArray();
    }

    private static async Task ConcluirAsync(HttpClient conferente, Guid id, bool aprovado)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{id}/pegar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{id}/iniciar", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await conferente.PostAsJsonAsync($"/minha-fila/{id}/concluir", new { aprovado })).StatusCode);
    }
}
