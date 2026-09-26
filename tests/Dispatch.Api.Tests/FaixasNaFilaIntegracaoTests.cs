using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Tests;

// A legenda "Prazo do ato" da Minha fila mostra os limites do semáforo da Configuração, e o
// Conferente puro não lê GET /config (é só gestão) — por isso as duas leituras de fila devolvem
// `faixas`. O que só a integração prova: o valor sai da linha `configuracao` do Postgres (via
// cache invalidado pelo PUT), não de um padrão fixo, e com o nome camelCase que o front lê.
//
// A linha `configuracao` NÃO é zerada entre testes (IntegracaoFixture) — o teste devolve os
// valores lidos no começo, no `finally`, como ConfiguracaoIntegracaoTests faz.
[Collection(IntegracaoCollection.Nome)]
public sealed class FaixasNaFilaIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    [Fact]
    public async Task MinhaFila_EFilaDoConferente_DevolvemAsFaixasDaConfiguracao()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var original = await admin.GetFromJsonAsync<JsonObject>("/config");
        try
        {
            // Padrão da migration: 240 min / 60 min.
            await AssertFaixasNasDuasFilasAsync(atencaoMinutos: 240, urgenteMinutos: 60);

            var alterada = original!.DeepClone().AsObject();
            alterada["faixaAtencaoMinutos"] = 150;
            alterada["faixaUrgenteMinutos"] = 45;
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/config", alterada)).StatusCode);

            await AssertFaixasNasDuasFilasAsync(atencaoMinutos: 150, urgenteMinutos: 45);
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/config", original)).StatusCode);
        }
    }

    private async Task AssertFaixasNasDuasFilasAsync(int atencaoMinutos, int urgenteMinutos)
    {
        var conferente = await AutenticarComoAsync(Papel.Conferente);
        var minhaFila = await conferente.GetFromJsonAsync<JsonElement>("/minha-fila");
        AssertFaixas(minhaFila, atencaoMinutos, urgenteMinutos);

        Guid conferenteId = default;
        await NoBancoAsync(async db => conferenteId = await db.Conferentes.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Id).FirstAsync());
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var filaDoConferente = await distribuidora.GetFromJsonAsync<JsonElement>($"/conferentes/{conferenteId}/fila");
        AssertFaixas(filaDoConferente, atencaoMinutos, urgenteMinutos);
    }

    private static void AssertFaixas(JsonElement fila, int atencaoMinutos, int urgenteMinutos)
    {
        var faixas = fila.GetProperty("faixas");
        Assert.Equal(atencaoMinutos, faixas.GetProperty("atencaoMinutos").GetInt32());
        Assert.Equal(urgenteMinutos, faixas.GetProperty("urgenteMinutos").GetInt32());
        // Aditivo: as três listas de antes continuam na resposta.
        Assert.Equal(JsonValueKind.Array, fila.GetProperty("poolDisponivel").ValueKind);
        Assert.Equal(JsonValueKind.Array, fila.GetProperty("atribuidos").ValueKind);
        Assert.Equal(JsonValueKind.Array, fila.GetProperty("emConferencia").ValueKind);
    }
}
