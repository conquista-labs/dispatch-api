using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Dispatch.Api.Tests;

[Collection(IntegracaoCollection.Nome)]
public sealed class RegraAlcadaIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    // Regressão do bug mais recorrente deste projeto (aconteceu em RegraAlcada e de novo em
    // Sugestao): a entidade de Domain devolvida pelo repositório é uma tradução nova do registro
    // achatado, não o objeto rastreado pelo EF — mutar ela e chamar SaveChanges não grava nada.
    // Teste com fake não pega isso: fake não tem change tracker pra perder a referência.
    [Fact]
    public async Task AtivarEDesativar_PersistemDeVerdadeNoBanco()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);
        var regraId = await CriarRegraAsync(cliente);

        Assert.True(await EstaAtivaAsync(cliente, regraId));

        var desativou = await cliente.PostAsync($"/regras-alcada/{regraId}/desativar", content: null);
        Assert.Equal(HttpStatusCode.NoContent, desativou.StatusCode);
        Assert.False(await EstaAtivaAsync(cliente, regraId));

        var ativou = await cliente.PostAsync($"/regras-alcada/{regraId}/ativar", content: null);
        Assert.Equal(HttpStatusCode.NoContent, ativou.StatusCode);
        Assert.True(await EstaAtivaAsync(cliente, regraId));
    }

    [Fact]
    public async Task DoisAlvosAoMesmoTempo_RejeitadoPelaApi()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);

        var resposta = await cliente.PostAsJsonAsync("/regras-alcada", new
        {
            sujeitoNivel = nameof(Nivel.Junior),
            permissao = nameof(PermissaoRegra.Nega),
            alvoEtapa = nameof(Etapa.PreConferencia),
            alvoTodosOsAtos = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
    }

    // Par do teste acima, um nível abaixo: confirma que o CHECK existe no Postgres de verdade,
    // não só a validação da Api. Os dois podem divergir com o tempo (um alvo novo entra no
    // endpoint e ninguém lembra do CHECK) — o banco é a última linha de defesa do invariante.
    [Fact]
    public async Task CheckDoBanco_RejeitaAlvoInconsistente_MesmoPorForaDaApi()
    {
        await NoBancoAsync(async db =>
        {
            var erro = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO regras_alcada
                    (id, sujeito_conferente_id, sujeito_nivel, alvo_tipo, alvo_etapa, alvo_tipo_ato_id,
                     alvo_equipe_id, alvo_grupo_tipo_ato, permissao, origem, ativa)
                VALUES
                    (gen_random_uuid(), NULL, 'Junior', 'Etapa', 'PreConferencia', gen_random_uuid(),
                     NULL, NULL, 'Nega', 'Manual', true)
                """));

            // 23514 = check_violation.
            Assert.Equal("23514", erro.SqlState);
        });
    }

    private static async Task<string> CriarRegraAsync(HttpClient cliente)
    {
        var resposta = await cliente.PostAsJsonAsync("/regras-alcada", new
        {
            sujeitoNivel = nameof(Nivel.Junior),
            permissao = nameof(PermissaoRegra.Nega),
            alvoEtapa = nameof(Etapa.PreConferencia),
        });

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        var criada = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        return criada.GetProperty("regraId").GetString()!;
    }

    private static async Task<bool> EstaAtivaAsync(HttpClient cliente, string regraId)
    {
        var regras = await cliente.GetFromJsonAsync<JsonElement>("/regras-alcada");
        var regra = regras.EnumerateArray().Single(r => r.GetProperty("id").GetString() == regraId);
        return regra.GetProperty("ativa").GetBoolean();
    }
}
