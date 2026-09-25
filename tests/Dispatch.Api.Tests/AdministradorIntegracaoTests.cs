using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;

namespace Dispatch.Api.Tests;

// Perfil Administrador (ADR-0039/ADR-0040) pelo pipeline real — fake nenhum simula RequireRole
// somando com a política do grupo, o middleware da troca de senha nem o token de conta desativada.
[Collection(IntegracaoCollection.Nome)]
public sealed class AdministradorIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private static readonly string IdQualquer = Guid.NewGuid().ToString();

    // A distribuidora é barrada pela autorização antes de o handler rodar — por isso um id
    // qualquer basta; o par "admin passa" está nos outros testes deste arquivo e em
    // AutorizacaoIntegracaoTests.
    // Exaustiva de propósito: toda rota da tabela "só Administrador" de docs/patterns/autorizacao.md.
    // Rota nova só do admin entra aqui.
    public static TheoryData<string, string> EscritasSoDoAdmin => new()
    {
        { "POST", "/conferentes" },
        { "POST", "/conferentes/vincular" },
        { "PUT", $"/conferentes/{IdQualquer}/perfil" },
        { "PUT", $"/conferentes/{IdQualquer}/nivel-jornada" },
        { "DELETE", $"/conferentes/{IdQualquer}" },
        { "GET", "/contas" },
        { "POST", "/contas" },
        { "POST", $"/contas/{IdQualquer}/desativar" },
        { "POST", "/regras-alcada" },
        { "POST", $"/regras-alcada/{IdQualquer}/ativar" },
        { "POST", $"/regras-alcada/{IdQualquer}/desativar" },
        { "DELETE", $"/regras-alcada/{IdQualquer}" },
        { "POST", "/regras-alcada/testar" },
        { "PUT", "/config" },
        { "POST", "/tipos-ato" },
        { "GET", "/tipos-ato/com-uso" },
        { "PUT", $"/tipos-ato/{IdQualquer}" },
        { "PUT", $"/tipos-ato/{IdQualquer}/peso" },
        { "PUT", $"/tipos-ato/{IdQualquer}/grupo" },
        { "POST", $"/tipos-ato/{IdQualquer}/ativar" },
        { "POST", $"/tipos-ato/{IdQualquer}/desativar" },
        { "DELETE", $"/tipos-ato/{IdQualquer}" },
        { "POST", "/equipes" },
        { "PUT", $"/equipes/{IdQualquer}" },
        { "GET", "/escreventes/sem-equipe" },
        { "POST", "/escreventes" },
        { "POST", $"/escreventes/{IdQualquer}/mover" },
        { "POST", "/sugestoes/gerar" },
        { "GET", "/sugestoes" },
        { "GET", "/sugestoes/historico" },
        { "POST", $"/sugestoes/{IdQualquer}/aplicar" },
        { "POST", $"/sugestoes/{IdQualquer}/descartar" },
    };

    [Theory]
    [MemberData(nameof(EscritasSoDoAdmin))]
    public async Task Distribuidora_LevaForbiddenNoQueEhSoDoAdmin(string metodo, string rota)
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);

        var resposta = await distribuidora.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), rota)
        {
            Content = metodo is "GET" or "DELETE" ? null : JsonContent.Create(new { }),
        });

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task Distribuidora_ContinuaMarcandoPresencaELendoRegrasEConfig()
    {
        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var conferenteId = await ConferenteIdAsync(distribuidora, "conferente-rf27@cartorio.com");

        var presenca = await distribuidora.PostAsJsonAsync($"/conferentes/{conferenteId}/presenca", new { presente = true });
        Assert.Equal(HttpStatusCode.NoContent, presenca.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await distribuidora.GetAsync("/regras-alcada")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await distribuidora.GetAsync("/config")).StatusCode);
    }

    [Fact]
    public async Task NivelSoApareceProAdmin_EmConferentesENasRegras()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var criada = await admin.PostAsJsonAsync("/regras-alcada", new
        {
            sujeitoNivel = nameof(Nivel.Junior),
            permissao = nameof(PermissaoRegra.Nega),
            alvoEtapa = nameof(Etapa.PreConferencia),
            alvoEhEquipe = false,
            alvoTodosOsAtos = false,
        });
        Assert.True(criada.IsSuccessStatusCode, await criada.Content.ReadAsStringAsync());
        var regraId = (await criada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("regraId").GetGuid();

        var comoAdmin = await admin.GetFromJsonAsync<JsonElement>("/regras-alcada");
        var regraProAdmin = comoAdmin.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == regraId);
        Assert.Equal(nameof(Nivel.Junior), regraProAdmin.GetProperty("sujeitoNivel").GetString());
        Assert.False(regraProAdmin.GetProperty("regraBase").GetBoolean());
        Assert.All(await ConferentesAsync(admin), c => Assert.NotEqual(JsonValueKind.Null, c.GetProperty("nivel").ValueKind));

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var comoDistribuidora = await distribuidora.GetFromJsonAsync<JsonElement>("/regras-alcada");
        var regraProDistribuidora = comoDistribuidora.EnumerateArray().Single(r => r.GetProperty("id").GetGuid() == regraId);
        Assert.Equal(JsonValueKind.Null, regraProDistribuidora.GetProperty("sujeitoNivel").ValueKind);
        Assert.True(regraProDistribuidora.GetProperty("regraBase").GetBoolean());
        Assert.All(await ConferentesAsync(distribuidora), c => Assert.Equal(JsonValueKind.Null, c.GetProperty("nivel").ValueKind));
    }

    [Fact]
    public async Task DashboardSemScoreProDistribuidora_ComScoreProAdmin()
    {
        await ConcluirUmProtocoloAsync();

        var admin = await AutenticarComoAsync(Papel.Administrador);
        var linhaAdmin = (await admin.GetFromJsonAsync<JsonElement>("/dashboard?periodo=Mes")).GetProperty("desempenho").EnumerateArray().First();
        Assert.Equal(JsonValueKind.Number, linhaAdmin.GetProperty("score").ValueKind);

        var distribuidora = await AutenticarComoAsync(Papel.Distribuidora);
        var linhaDistribuidora = (await distribuidora.GetFromJsonAsync<JsonElement>("/dashboard?periodo=Mes"))
            .GetProperty("desempenho").EnumerateArray().First();
        Assert.Equal(JsonValueKind.Null, linhaDistribuidora.GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Null, linhaDistribuidora.GetProperty("nivel").ValueKind);
        Assert.Equal(JsonValueKind.Null, linhaDistribuidora.GetProperty("faixa").ValueKind);
    }

    // RF-45/46/47 ponta a ponta: conta criada pelo admin → login com a senha inicial → token só
    // serve pra trocar a senha → troca → usa o sistema → admin desativa → o token dela para de valer.
    [Fact]
    public async Task ContaNova_TrocaSenhaNoPrimeiroAcesso_EDesativadaPerdeOAcesso()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var criada = await admin.PostAsJsonAsync("/contas", new
        {
            nome = "Distribuidora Nova",
            email = "nova@cartorio.com",
            senhaInicial = "abcd-efgh-12",
            papel = nameof(Papel.Distribuidora),
        });
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);
        var contaId = (await criada.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("usuarioId").GetGuid();

        var nova = CriarCliente();
        var login = await nova.PostAsJsonAsync("/auth/login", new { email = "nova@cartorio.com", senha = "abcd-efgh-12" });
        var corpoLogin = await login.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(corpoLogin.GetProperty("usuario").GetProperty("trocarSenha").GetBoolean());
        nova.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", corpoLogin.GetProperty("token").GetString());

        var barrada = await nova.GetAsync("/protocolos/distribuicao");
        Assert.Equal(HttpStatusCode.Forbidden, barrada.StatusCode);
        Assert.Contains("troca_de_senha_obrigatoria", await barrada.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await nova.GetAsync("/auth/me")).StatusCode);

        var troca = await nova.PostAsJsonAsync("/auth/trocar-senha", new { senhaAtual = "abcd-efgh-12", novaSenha = "uma frase longa e boa" });
        Assert.Equal(HttpStatusCode.OK, troca.StatusCode);
        var tokenNovo = (await troca.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        nova.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenNovo);
        Assert.Equal(HttpStatusCode.OK, (await nova.GetAsync("/protocolos/distribuicao")).StatusCode);

        admin = await LogarAsync(CriarCliente(), "administrador@cartorio.com", "Senha123!");
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/contas/{contaId}/desativar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await nova.GetAsync("/protocolos/distribuicao")).StatusCode);
    }

    [Fact]
    public async Task Admin_NaoDesativaAPropriaConta()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var contas = await admin.GetFromJsonAsync<JsonElement>("/contas");
        var minhaId = contas.EnumerateArray().Single(c => c.GetProperty("ehVoce").GetBoolean()).GetProperty("id").GetGuid();

        var resposta = await admin.PostAsync($"/contas/{minhaId}/desativar", null);

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
        Assert.Contains("propria_e_ultimo_administrador", await resposta.Content.ReadAsStringAsync());
    }

    private static async Task<IReadOnlyList<JsonElement>> ConferentesAsync(HttpClient cliente) =>
        (await cliente.GetFromJsonAsync<JsonElement>("/conferentes")).EnumerateArray().ToList();

    private static async Task<Guid> ConferenteIdAsync(HttpClient cliente, string email) =>
        (await ConferentesAsync(cliente)).Single(c => c.GetProperty("email").GetString() == email).GetProperty("id").GetGuid();

    // Um protocolo concluído pela conferente seed, pra o Dashboard ter uma linha de desempenho.
    private async Task ConcluirUmProtocoloAsync()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var agora = DateTimeOffset.UtcNow;
        var importou = await admin.PostAsJsonAsync("/protocolos/importar/confirmar", new
        {
            etapa = nameof(Etapa.PosConferencia),
            linhaDeCorte = agora.AddDays(-1),
            linhas = new[] { new { protocolo = "ADMIN-E2E-1", tipoAto = "Ato Admin E2E", escrevente = "Escrevente Admin E2E", dataHoraAndamento = agora.AddHours(-2) } },
        });
        Assert.True(importou.IsSuccessStatusCode, await importou.Content.ReadAsStringAsync());
        var visao = await admin.GetFromJsonAsync<JsonElement>("/protocolos/distribuicao");
        var protocoloId = visao.GetProperty("pool").EnumerateArray()
            .Single(p => p.GetProperty("numero").GetString() == "ADMIN-E2E-1").GetProperty("id").GetGuid();

        var conferente = await AutenticarComoAsync(Papel.Conferente);
        Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{protocoloId}/pegar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await conferente.PostAsync($"/minha-fila/{protocoloId}/iniciar", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await conferente.PostAsJsonAsync($"/minha-fila/{protocoloId}/concluir", new { aprovado = true })).StatusCode);
    }
}
