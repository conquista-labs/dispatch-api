using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;

namespace Dispatch.Api.Tests;

// O card de exceção "tipo desconhecido" (RF-09) só tem o nome do tipo como veio no relatório —
// TipoAtoId é nulo, então o front não tem o que resolver pelo catálogo. ProtocoloResumo passa a
// levar TipoAtoNomeOriginal, e este teste prova que o valor gravado chega em
// GET /protocolos/distribuicao. Desde o ADR-0012 a importação cadastra o tipo novo, então o
// protocolo com tipo nulo + nome original é semeado direto no banco (é o dado legado, anterior
// ao ADR-0012, que ainda existe em produção).
[Collection(IntegracaoCollection.Nome)]
public sealed class TipoAtoNomeOriginalIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private static readonly DateTimeOffset Andamento = new(2026, 9, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExcecaoDeTipoDesconhecido_TrazONomeOriginalNaVisaoDaDistribuicao()
    {
        var protocoloId = Guid.NewGuid();
        await NoBancoAsync(async db =>
        {
            var escrevente = new Escrevente(Guid.NewGuid(), "Escrevente Legado E2E", equipeId: null);
            var protocolo = new Protocolo(
                protocoloId, "LEGADO-E2E-1", tipoAtoId: null, escrevente.Id, Etapa.PosConferencia, Andamento,
                tipoAtoNomeOriginal: "ESCRITURA DE CESSAO DE DIREITOS");
            protocolo.MarcarExcecao("tipo desconhecido");
            db.Escreventes.Add(escrevente);
            db.Protocolos.Add(protocolo);
            await db.SaveChangesAsync();
        });

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        // Tipo conhecido (a importação cadastra) — o campo vem nulo, mesma regra do Domain.
        var importacao = await distribuidora.PostAsJsonAsync("/protocolos/importar/confirmar", new
        {
            etapa = nameof(Etapa.PosConferencia),
            linhaDeCorte = Andamento.AddDays(-1),
            linhas = new[] { new { protocolo = "CONHECIDO-E2E-1", tipoAto = "Venda e Compra", escrevente = "Ana Souza", dataHoraAndamento = Andamento } },
        });
        Assert.True(importacao.IsSuccessStatusCode, await importacao.Content.ReadAsStringAsync());

        var visao = await distribuidora.GetFromJsonAsync<JsonElement>("/protocolos/distribuicao");

        var excecao = visao.GetProperty("excecoes").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == protocoloId);
        Assert.Equal(JsonValueKind.Null, excecao.GetProperty("tipoAtoId").ValueKind);
        Assert.Equal("ESCRITURA DE CESSAO DE DIREITOS", excecao.GetProperty("tipoAtoNomeOriginal").GetString());

        var conhecido = TodosOsResumos(visao).Single(p => p.GetProperty("numero").GetString() == "CONHECIDO-E2E-1");
        Assert.NotEqual(JsonValueKind.Null, conhecido.GetProperty("tipoAtoId").ValueKind);
        Assert.Equal(JsonValueKind.Null, conhecido.GetProperty("tipoAtoNomeOriginal").ValueKind);
    }

    private static IEnumerable<JsonElement> TodosOsResumos(JsonElement visao) =>
        new[] { "pool", "atribuidos", "emConferencia", "concluidos", "excecoes" }
            .SelectMany(bucket => visao.GetProperty(bucket).EnumerateArray());
}
