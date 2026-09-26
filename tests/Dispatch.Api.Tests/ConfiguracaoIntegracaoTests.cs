using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Tests;

// Metas (RF-42b) e pesos do score (RF-46) na Configuração, pelo pipeline real: colunas novas lidas do
// Postgres, os 6 campos opcionais no PUT (o corpo do front anterior continua valendo), 400 com motivo,
// o cache da configuração invalidado e o RequireRole do Administrador.
//
// A linha `configuracao` NÃO é zerada entre testes (IntegracaoFixture: é semeada pela migration) —
// todo teste que muda valor devolve a linha ao que leu no começo, no `finally`.
[Collection(IntegracaoCollection.Nome)]
public sealed class ConfiguracaoIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    [Fact]
    public async Task Get_DevolveMetasEPesosComOsPadroesDaMigration()
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);

        var config = await distribuidora.GetFromJsonAsync<JsonElement>("/config");

        Assert.Equal(0.95, config.GetProperty("metaNoPrazo").GetDouble());
        Assert.Equal(0.90, config.GetProperty("metaAprovadoNaPrimeira").GetDouble());
        Assert.Equal(40, config.GetProperty("pesoVolume").GetInt32());
        Assert.Equal(30, config.GetProperty("pesoPrazo").GetInt32());
        Assert.Equal(20, config.GetProperty("pesoQualidade").GetInt32());
        Assert.Equal(10, config.GetProperty("pesoComplexidade").GetInt32());
        // Os 12 de antes continuam lá.
        Assert.Equal(240, config.GetProperty("faixaAtencaoMinutos").GetInt32());
    }

    [Fact]
    public async Task Put_SemOsCamposNovos_MantemMetasEPesos_ComEles_Grava()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var original = await admin.GetFromJsonAsync<JsonObject>("/config");
        try
        {
            // 1) Grava metas/pesos diferentes dos padrões.
            var comNovos = CorpoDoFrontAnterior(original!);
            comNovos["metaNoPrazo"] = 0.85;
            comNovos["metaAprovadoNaPrimeira"] = 0.8;
            comNovos["pesoVolume"] = 25;
            comNovos["pesoPrazo"] = 25;
            comNovos["pesoQualidade"] = 25;
            comNovos["pesoComplexidade"] = 25;
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/config", comNovos)).StatusCode);

            // 2) O front anterior salva só os 12 (e muda um deles) — metas/pesos ficam como estavam.
            var semNovos = CorpoDoFrontAnterior(original!);
            semNovos["faixaAtencaoMinutos"] = 180;
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/config", semNovos)).StatusCode);

            var lida = await admin.GetFromJsonAsync<JsonElement>("/config");
            Assert.Equal(180, lida.GetProperty("faixaAtencaoMinutos").GetInt32());
            Assert.Equal(0.85, lida.GetProperty("metaNoPrazo").GetDouble());
            Assert.Equal(0.8, lida.GetProperty("metaAprovadoNaPrimeira").GetDouble());
            Assert.Equal(25, lida.GetProperty("pesoVolume").GetInt32());
            Assert.Equal(25, lida.GetProperty("pesoComplexidade").GetInt32());

            // Gravado no Postgres, não só no cache.
            await NoBancoAsync(async db =>
            {
                var linha = await db.Configuracoes.AsNoTracking().SingleAsync();
                Assert.Equal(new PesosDoScore(25, 25, 25, 25), linha.Pesos);
                Assert.Equal(new MetasDoDashboard(0.85, 0.8), linha.Metas);
            });
        }
        finally
        {
            await RestaurarAsync(admin, original!);
        }
    }

    [Theory]
    [InlineData("{\"pesoVolume\":50}", "somar 100")]
    [InlineData("{\"pesoVolume\":50,\"pesoPrazo\":30,\"pesoQualidade\":30,\"pesoComplexidade\":-10}", "pesoComplexidade")]
    [InlineData("{\"metaNoPrazo\":95}", "metaNoPrazo")]
    [InlineData("{\"metaAprovadoNaPrimeira\":0.3}", "metaAprovadoNaPrimeira")]
    public async Task Put_MetasOuPesosInvalidos_400ComMotivo_ENadaMuda(string camposNovos, string trechoDoMotivo)
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var original = await admin.GetFromJsonAsync<JsonObject>("/config");
        try
        {
            var corpo = CorpoDoFrontAnterior(original!);
            corpo["faixaAtencaoMinutos"] = 180;
            foreach (var (campo, valor) in JsonNode.Parse(camposNovos)!.AsObject())
            {
                corpo[campo] = valor!.DeepClone();
            }

            var resposta = await admin.PutAsJsonAsync("/config", corpo);

            Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
            var motivo = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("motivo").GetString();
            Assert.Contains(trechoDoMotivo, motivo);
            var lida = await admin.GetFromJsonAsync<JsonElement>("/config");
            Assert.Equal(original!["faixaAtencaoMinutos"]!.GetValue<int>(), lida.GetProperty("faixaAtencaoMinutos").GetInt32());
            Assert.Equal(40, lida.GetProperty("pesoVolume").GetInt32());
        }
        finally
        {
            await RestaurarAsync(admin, original!);
        }
    }

    [Fact]
    public async Task Put_Distribuidora_LevaForbiddenMesmoComCorpoValido()
    {
        // O par que passa (admin, 204) está no teste acima; aqui o corpo é válido de propósito, pra o
        // 403 ser sobre papel e não sobre request malformado.
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var original = await admin.GetFromJsonAsync<JsonObject>("/config");
        var corpo = CorpoDoFrontAnterior(original!);
        corpo["pesoVolume"] = 30;
        corpo["pesoComplexidade"] = 20;

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var resposta = await distribuidora.PutAsJsonAsync("/config", corpo);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(40, (await distribuidora.GetFromJsonAsync<JsonElement>("/config")).GetProperty("pesoVolume").GetInt32());
    }

    private static readonly string[] CamposNovos =
        ["metaNoPrazo", "metaAprovadoNaPrimeira", "pesoVolume", "pesoPrazo", "pesoQualidade", "pesoComplexidade",
         "limiteDeAtosNaMao", "poolEmOrdemObrigatoria"];

    // O corpo que o front anterior manda: os 12 de sempre, sem nenhum dos opcionais (metas, pesos, regra do pool).
    private static JsonObject CorpoDoFrontAnterior(JsonObject lido)
    {
        var corpo = lido.DeepClone().AsObject();
        foreach (var campo in CamposNovos)
        {
            corpo.Remove(campo);
        }

        return corpo;
    }

    // GET e PUT usam os mesmos nomes — devolver o que foi lido restaura os 20 valores.
    private static async Task RestaurarAsync(HttpClient admin, JsonObject original)
    {
        var resposta = await admin.PutAsJsonAsync("/config", original);
        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
    }
}
