using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;

namespace Dispatch.Api.Tests;

// RF-42a — GET /dashboard/hoje pelo pipeline real: o que fake não prova é a visão escolhida pelo
// papel do token (RequireRole do grupo + IsInRole no handler), o conferente resolvido pelo usuário
// logado, o caso de uso registrado no DI e a forma do JSON (camelCase, enum como string, campos
// nulos presentes). Os números vêm de um fluxo de verdade: importar → pegar → iniciar → concluir.
[Collection(IntegracaoCollection.Nome)]
public sealed class PainelDeHojeIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private const string Rota = "/dashboard/hoje";

    [Fact]
    public async Task SemToken_Leva401()
    {
        var resposta = await CriarCliente().GetAsync(Rota);

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
    }

    [Fact]
    public async Task Distribuidora_RecebeVisaoDeGestao_EConferente_RecebeSeuDia()
    {
        // Três protocolos de um escrevente novo (sem equipe → prazo D+1) com andamento de 10 dias
        // atrás: todos já estourados. O conferente conclui um e fica com outro na mão.
        var numeros = await ImportarEstouradosAsync(3);
        var conferente = await AutenticarComoAsync(Papel.Conferente);
        var ids = await IdsNoPoolAsync(numeros);

        await EsperarSucessoAsync(conferente.PostAsync($"/minha-fila/{ids[0]}/pegar", null));
        await EsperarSucessoAsync(conferente.PostAsync($"/minha-fila/{ids[0]}/iniciar", null));
        await EsperarSucessoAsync(conferente.PostAsJsonAsync($"/minha-fila/{ids[0]}/concluir", new { aprovado = true }));
        await EsperarSucessoAsync(conferente.PostAsync($"/minha-fila/{ids[1]}/pegar", null));

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var gestao = await distribuidora.GetFromJsonAsync<JsonElement>(Rota);

        Assert.Equal("Gestao", gestao.GetProperty("visao").GetString());
        Assert.Equal(1, gestao.GetProperty("conferidosHoje").GetInt32());
        Assert.Equal(1, gestao.GetProperty("naFila").GetProperty("pool").GetInt32());
        Assert.Equal(1, gestao.GetProperty("naFila").GetProperty("comConferente").GetInt32());
        Assert.Equal(JsonValueKind.Null, gestao.GetProperty("naMao").ValueKind);
        Assert.Equal(2, gestao.GetProperty("emRisco").GetProperty("estourados").GetInt32());
        Assert.Equal(0, gestao.GetProperty("emRisco").GetProperty("vencemEmUmaHora").GetInt32());
        Assert.Equal(0, gestao.GetProperty("excecoes").GetInt32());
        // Escrevente sem equipe: o grupo "sem equipe" concentra os 2 em risco.
        Assert.Equal(JsonValueKind.Null, gestao.GetProperty("gargalo").GetProperty("equipeId").ValueKind);
        Assert.Equal(2, gestao.GetProperty("gargalo").GetProperty("quantidade").GetInt32());
        Assert.True(gestao.GetProperty("atualizadoEm").GetDateTimeOffset() > DateTimeOffset.UtcNow.AddMinutes(-5));

        var seuDia = await conferente.GetFromJsonAsync<JsonElement>(Rota);

        Assert.Equal("Conferente", seuDia.GetProperty("visao").GetString());
        Assert.Equal(1, seuDia.GetProperty("conferidosHoje").GetInt32());
        Assert.Equal(1, seuDia.GetProperty("naMao").GetProperty("total").GetInt32());
        Assert.Equal(0, seuDia.GetProperty("naMao").GetProperty("emConferencia").GetInt32());
        Assert.Equal(1, seuDia.GetProperty("emRisco").GetProperty("estourados").GetInt32());
        Assert.Equal(JsonValueKind.Null, seuDia.GetProperty("naFila").ValueKind);
        Assert.Equal(JsonValueKind.Null, seuDia.GetProperty("excecoes").ValueKind);
        Assert.Equal(JsonValueKind.Null, seuDia.GetProperty("gargalo").ValueKind);
    }

    [Fact]
    public async Task Administrador_RecebeVisaoDeGestao()
    {
        // O Administrador carrega também a claim Distribuidora (ADR-0039) — nunca cai na visão restrita.
        var administrador = await AutenticarComoAsync(Papel.Administrador);

        var painel = await administrador.GetFromJsonAsync<JsonElement>(Rota);

        Assert.Equal("Gestao", painel.GetProperty("visao").GetString());
        Assert.Equal(JsonValueKind.Null, painel.GetProperty("gargalo").ValueKind);
    }

    private async Task<string[]> ImportarEstouradosAsync(int quantidade)
    {
        var andamento = DateTimeOffset.UtcNow.AddDays(-10);
        var numeros = Enumerable.Range(1, quantidade).Select(i => $"HOJE-E2E-{i}").ToArray();
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var resposta = await distribuidora.PostAsJsonAsync("/protocolos/importar/confirmar", new
        {
            etapa = nameof(Etapa.PosConferencia),
            linhaDeCorte = andamento.AddDays(-1),
            linhas = numeros.Select(n => new
            {
                protocolo = n,
                tipoAto = "Ato Painel E2E",
                escrevente = "Escrevente Painel E2E",
                dataHoraAndamento = andamento,
            }).ToArray(),
        });
        Assert.True(resposta.IsSuccessStatusCode, $"status={resposta.StatusCode} corpo={await resposta.Content.ReadAsStringAsync()}");
        return numeros;
    }

    private async Task<Guid[]> IdsNoPoolAsync(string[] numeros)
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var visao = await distribuidora.GetFromJsonAsync<JsonElement>("/protocolos/distribuicao");
        var pool = visao.GetProperty("pool").EnumerateArray().ToList();
        return numeros
            .Select(n => pool.Single(p => p.GetProperty("numero").GetString() == n).GetProperty("id").GetGuid())
            .ToArray();
    }

    private static async Task EsperarSucessoAsync(Task<HttpResponseMessage> chamada)
    {
        var resposta = await chamada;
        Assert.True(
            resposta.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK,
            $"status={resposta.StatusCode} corpo={await resposta.Content.ReadAsStringAsync()}");
    }
}
