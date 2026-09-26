using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Tests;

// Regra do pool (ADR-0046) pelo pipeline real: as colunas novas da `configuracao` lidas do Postgres,
// `regraDoPool` nas duas leituras de fila, os 409 com `codigo` do pegar (limite_na_mao, fora_da_vez),
// a chave desligada, a corrida pelo mesmo primeiro, o escrevente escondido do conferente no pool e nas
// atribuídas, e o PUT /config dos campos novos (400 com motivo, 403 de quem não é Administrador).
//
// A linha `configuracao` NÃO é zerada entre testes (IntegracaoFixture) — quem muda devolve no `finally`.
[Collection(IntegracaoCollection.Nome)]
public sealed class PoolEmOrdemIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private static readonly DateTimeOffset Andamento = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private sealed record PoolSemeado(Guid EscreventeId, Guid Alta, Guid VenceAntes, Guid VenceDepois, Guid SemVencimento);

    // Quatro atos no pool, inseridos fora de ordem, que a vez precisa pôr em: Alta (vence por último,
    // mas é Alta) → vence antes → vence depois → sem vencimento. Sem regra de alçada = todos visíveis.
    private async Task<PoolSemeado> SemearPoolAsync()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Ato Pool em Ordem");
        var escrevente = new Escrevente(Guid.NewGuid(), "Escrevente Pool em Ordem", equipeId: null);

        Protocolo Novo(string numero, Prioridade prioridade, int? venceEmHoras)
        {
            var protocolo = new Protocolo(Guid.NewGuid(), numero, tipo.Id, escrevente.Id, Etapa.PosConferencia, Andamento, prioridade);
            if (venceEmHoras is { } horas)
            {
                protocolo.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), Andamento.AddHours(horas - 1));
            }

            return protocolo;
        }

        var semVencimento = Novo("POOL-4", Prioridade.Normal, null);
        var venceDepois = Novo("POOL-3", Prioridade.Normal, 10);
        var alta = Novo("POOL-1", Prioridade.Alta, 48);
        var venceAntes = Novo("POOL-2", Prioridade.Normal, 2);

        await NoBancoAsync(async db =>
        {
            db.TiposAto.Add(tipo);
            db.Escreventes.Add(escrevente);
            db.Protocolos.AddRange(semVencimento, venceDepois, alta, venceAntes);
            await db.SaveChangesAsync();
        });

        return new PoolSemeado(escrevente.Id, alta.Id, venceAntes.Id, venceDepois.Id, semVencimento.Id);
    }

    [Fact]
    public async Task MinhaFila_VemNaOrdemDaVez_ComRegraDoPool_ESemEscreventeNoPool()
    {
        var conferente = await AutenticarComoAsync(Papel.Conferente);
        var pool = await SemearPoolAsync();

        var fila = await conferente.GetFromJsonAsync<JsonElement>("/minha-fila");

        Assert.Equal([pool.Alta, pool.VenceAntes, pool.VenceDepois, pool.SemVencimento], Ids(fila, "poolDisponivel"));
        AssertRegra(fila, ordemObrigatoria: true, limiteNaMao: 5, naMao: 0, proximoId: pool.Alta);
        Assert.All(fila.GetProperty("poolDisponivel").EnumerateArray(),
            p => Assert.Equal(JsonValueKind.Null, p.GetProperty("escreventeId").ValueKind));
    }

    [Fact]
    public async Task Pegar_ForaDaVez_409ComCodigo_OPrimeiroPassa_EOSegundoConferenteRecebeNaoEstaNoPool()
    {
        var conferente = await AutenticarComoAsync(Papel.Conferente);
        var pool = await SemearPoolAsync();

        var foraDaVez = await conferente.PostAsync($"/minha-fila/{pool.VenceAntes}/pegar", null);
        await AssertConflitoComCodigoAsync(foraDaVez, "fora_da_vez", "primeiro");

        var naVez = await conferente.PostAsync($"/minha-fila/{pool.Alta}/pegar", null);
        Assert.Equal(HttpStatusCode.NoContent, naVez.StatusCode);

        // Corrida: o outro conferente chega depois no mesmo "primeiro" — recebe o 409 que já existia
        // (sem `codigo`), e o próximo da vez dele já é o seguinte.
        var outro = await LogarAsync(CriarCliente(), "conferente-visual@cartorio.com", "Senha123!");
        var perdeuACorrida = await outro.PostAsync($"/minha-fila/{pool.Alta}/pegar", null);
        Assert.Equal(HttpStatusCode.Conflict, perdeuACorrida.StatusCode);
        var corpo = await perdeuACorrida.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("protocolo não está no pool", corpo.GetProperty("motivo").GetString());
        Assert.False(corpo.TryGetProperty("codigo", out _));
        AssertRegra(await outro.GetFromJsonAsync<JsonElement>("/minha-fila"), true, 5, 0, pool.VenceAntes);

        // Quem pegou: 1 na mão, o atribuído também sem escrevente; depois de iniciar, o escrevente aparece.
        var fila = await conferente.GetFromJsonAsync<JsonElement>("/minha-fila");
        AssertRegra(fila, true, 5, naMao: 1, proximoId: pool.VenceAntes);
        var atribuido = Assert.Single(fila.GetProperty("atribuidos").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, atribuido.GetProperty("escreventeId").ValueKind);

        Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{pool.Alta}/iniciar", null)).StatusCode);
        fila = await conferente.GetFromJsonAsync<JsonElement>("/minha-fila");
        var emConferencia = Assert.Single(fila.GetProperty("emConferencia").EnumerateArray());
        Assert.Equal(pool.EscreventeId, emConferencia.GetProperty("escreventeId").GetGuid());
        AssertRegra(fila, true, 5, naMao: 1, proximoId: pool.VenceAntes);

        await NoBancoAsync(async db =>
        {
            var pego = await db.Protocolos.AsNoTracking().SingleAsync(p => p.Id == pool.Alta);
            Assert.Equal(StatusProtocolo.Conferindo, pego.Status);
            var foraDaVezNoBanco = await db.Protocolos.AsNoTracking().SingleAsync(p => p.Id == pool.VenceDepois);
            Assert.Equal(StatusProtocolo.Pool, foraDaVezNoBanco.Status);
        });
    }

    [Fact]
    public async Task FilaDoConferente_Gestao_VeRegraDoPool_EOEscreventeEmTodasAsColunas()
    {
        var conferente = await AutenticarComoAsync(Papel.Conferente);
        var pool = await SemearPoolAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{pool.Alta}/pegar", null)).StatusCode);

        Guid conferenteId = default;
        await NoBancoAsync(async db => conferenteId = await db.Conferentes.AsNoTracking()
            .Where(c => db.Usuarios.Any(u => u.Id == c.UsuarioId && u.Email == "conferente-rf27@cartorio.com"))
            .Select(c => c.Id).SingleAsync());
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);

        var fila = await distribuidora.GetFromJsonAsync<JsonElement>($"/conferentes/{conferenteId}/fila");

        Assert.Equal([pool.VenceAntes, pool.VenceDepois, pool.SemVencimento], Ids(fila, "poolDisponivel"));
        AssertRegra(fila, true, 5, naMao: 1, proximoId: pool.VenceAntes);
        Assert.All(fila.GetProperty("poolDisponivel").EnumerateArray(),
            p => Assert.Equal(pool.EscreventeId, p.GetProperty("escreventeId").GetGuid()));
        Assert.Equal(pool.EscreventeId, Assert.Single(fila.GetProperty("atribuidos").EnumerateArray()).GetProperty("escreventeId").GetGuid());
    }

    [Fact]
    public async Task Config_LimiteNaMao_Trava409ComCodigo_ChaveDesligada_LiberaQualquerDoPool()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var original = await admin.GetFromJsonAsync<JsonObject>("/config");
        try
        {
            // Padrões da migration.
            Assert.Equal(5, original!["limiteDeAtosNaMao"]!.GetValue<int>());
            Assert.True(original["poolEmOrdemObrigatoria"]!.GetValue<bool>());

            var conferente = await AutenticarComoAsync(Papel.Conferente);
            var pool = await SemearPoolAsync();
            Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{pool.Alta}/pegar", null)).StatusCode);

            // Limite 1: com 1 na mão, nem o primeiro da vez passa.
            await PutConfigAsync(admin, original, limiteDeAtosNaMao: 1, poolEmOrdemObrigatoria: true);
            var fila = await conferente.GetFromJsonAsync<JsonElement>("/minha-fila");
            AssertRegra(fila, true, limiteNaMao: 1, naMao: 1, proximoId: null);
            var maoCheia = await conferente.PostAsync($"/minha-fila/{pool.VenceAntes}/pegar", null);
            await AssertConflitoComCodigoAsync(maoCheia, "limite_na_mao", "limite 1");

            // Chave desligada, limite de volta a 5: pega o último da ordem.
            await PutConfigAsync(admin, original, limiteDeAtosNaMao: 5, poolEmOrdemObrigatoria: false);
            fila = await conferente.GetFromJsonAsync<JsonElement>("/minha-fila");
            // Com a chave desligada, proximoId continua sendo o primeiro da ordem (documentado no ADR).
            AssertRegra(fila, ordemObrigatoria: false, limiteNaMao: 5, naMao: 1, proximoId: pool.VenceAntes);
            Assert.Equal(HttpStatusCode.NoContent,
                (await conferente.PostAsync($"/minha-fila/{pool.SemVencimento}/pegar", null)).StatusCode);

            await NoBancoAsync(async db =>
            {
                var linha = await db.Configuracoes.AsNoTracking().SingleAsync();
                Assert.Equal(5, linha.LimiteDeAtosNaMao);
                Assert.False(linha.PoolEmOrdemObrigatoria);
            });
        }
        finally
        {
            Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/config", original)).StatusCode);
        }
    }

    [Fact]
    public async Task PutConfig_LimiteNaMaoZero_400ComMotivo_ENadaMuda()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var original = await admin.GetFromJsonAsync<JsonObject>("/config");
        var corpo = original!.DeepClone().AsObject();
        corpo["limiteDeAtosNaMao"] = 0;
        corpo["poolEmOrdemObrigatoria"] = false;

        var resposta = await admin.PutAsJsonAsync("/config", corpo);

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains("limiteDeAtosNaMao", (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("motivo").GetString());
        var lida = await admin.GetFromJsonAsync<JsonElement>("/config");
        Assert.Equal(5, lida.GetProperty("limiteDeAtosNaMao").GetInt32());
        Assert.True(lida.GetProperty("poolEmOrdemObrigatoria").GetBoolean());
    }

    [Fact]
    public async Task PutConfig_RegraDoPool_Distribuidora403()
    {
        // O par que passa (admin, 204) é o teste acima de limite/chave; corpo válido aqui de propósito.
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var original = await admin.GetFromJsonAsync<JsonObject>("/config");
        var corpo = original!.DeepClone().AsObject();
        corpo["limiteDeAtosNaMao"] = 2;

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var resposta = await distribuidora.PutAsJsonAsync("/config", corpo);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
        Assert.Equal(5, (await distribuidora.GetFromJsonAsync<JsonElement>("/config")).GetProperty("limiteDeAtosNaMao").GetInt32());
    }

    private static async Task PutConfigAsync(HttpClient admin, JsonObject original, int limiteDeAtosNaMao, bool poolEmOrdemObrigatoria)
    {
        var corpo = original.DeepClone().AsObject();
        corpo["limiteDeAtosNaMao"] = limiteDeAtosNaMao;
        corpo["poolEmOrdemObrigatoria"] = poolEmOrdemObrigatoria;
        var resposta = await admin.PutAsJsonAsync("/config", corpo);
        Assert.True(resposta.StatusCode == HttpStatusCode.NoContent, $"status={resposta.StatusCode} corpo={await resposta.Content.ReadAsStringAsync()}");
    }

    private static List<Guid> Ids(JsonElement fila, string coluna) =>
        fila.GetProperty(coluna).EnumerateArray().Select(p => p.GetProperty("id").GetGuid()).ToList();

    private static void AssertRegra(JsonElement fila, bool ordemObrigatoria, int limiteNaMao, int naMao, Guid? proximoId)
    {
        var regra = fila.GetProperty("regraDoPool");
        Assert.Equal(ordemObrigatoria, regra.GetProperty("ordemObrigatoria").GetBoolean());
        Assert.Equal(limiteNaMao, regra.GetProperty("limiteNaMao").GetInt32());
        Assert.Equal(naMao, regra.GetProperty("naMao").GetInt32());
        var proximo = regra.GetProperty("proximoId");
        Assert.Equal(proximoId, proximo.ValueKind == JsonValueKind.Null ? null : proximo.GetGuid());
    }

    private static async Task AssertConflitoComCodigoAsync(HttpResponseMessage resposta, string codigo, string trechoDoMotivo)
    {
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.True(resposta.StatusCode == HttpStatusCode.Conflict, $"status={resposta.StatusCode} corpo={texto}");
        var corpo = JsonDocument.Parse(texto).RootElement;
        Assert.Equal(codigo, corpo.GetProperty("codigo").GetString());
        Assert.Contains(trechoDoMotivo, corpo.GetProperty("motivo").GetString());
    }
}
