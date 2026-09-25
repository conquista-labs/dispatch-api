using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dispatch.Domain;
using Dispatch.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Dispatch.Api.Tests;

// Fatia 5 do Dashboard v2 (RF-34a, RF-34f, RF-46c, RF-18a) pelo pipeline real: peso decimal no JSON e
// no numeric(3,2), o PUT novo de tempo de referência com os CHECK do banco, e a migration convertendo
// os pesos inteiros que já existiam.
[Collection(IntegracaoCollection.Nome)]
public sealed class TipoAtoTempoDeReferenciaIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private const string MigrationAnterior = "20260925191850_AdicionaMetasEPesosEmConfiguracao";

    [Fact]
    public async Task PesoDecimal_ETempoInformado_IdaEVoltaPeloPostgres()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var criado = await admin.PostAsJsonAsync("/tipos-ato", new { nome = "Inventário Tempo E2E" });
        var tipoId = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tipoAtoId").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/tipos-ato/{tipoId}/peso", new { peso = 1.35m })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/tipos-ato/{tipoId}/tempo-referencia", new { minutos = 38 })).StatusCode);

        // GET /tipos-ato (distribuidora e conferente): o peso pro painel de detalhe (RF-18a).
        var conferente = await AutenticarComoAsync(Papel.Conferente);
        var catalogo = await conferente.GetFromJsonAsync<JsonElement>("/tipos-ato");
        var noCatalogo = catalogo.EnumerateArray().Single(t => t.GetProperty("id").GetGuid() == tipoId);
        Assert.Equal(1.35m, noCatalogo.GetProperty("pesoComplexidade").GetDecimal());

        var comUso = await admin.GetFromJsonAsync<JsonElement>("/tipos-ato/com-uso?busca=Tempo E2E");
        var item = comUso.GetProperty("itens").EnumerateArray().Single();
        Assert.Equal(1.35m, item.GetProperty("pesoComplexidade").GetDecimal());
        var tempo = item.GetProperty("tempoReferencia");
        Assert.Equal(38, tempo.GetProperty("minutos").GetInt32());
        Assert.Equal("Informado", tempo.GetProperty("origem").GetString());
        Assert.Equal(38, tempo.GetProperty("informadoMinutos").GetInt32());
        Assert.Equal(JsonValueKind.Null, tempo.GetProperty("medianaMinutos").ValueKind);
        Assert.Equal(0, tempo.GetProperty("conferenciasNoHistorico").GetInt32());

        // "usar histórico" sem histórico: volta pra estimativa = round(TempoMedioPorAtoMinutos × 1,35).
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/tipos-ato/{tipoId}/tempo-referencia", new { minutos = (int?)null })).StatusCode);
        var config = await admin.GetFromJsonAsync<JsonElement>("/config");
        var estimativa = (int)Math.Round(config.GetProperty("tempoMedioPorAtoMinutos").GetDouble() * 1.35, MidpointRounding.AwayFromZero);
        var depois = (await admin.GetFromJsonAsync<JsonElement>("/tipos-ato/com-uso?busca=Tempo E2E"))
            .GetProperty("itens")[0].GetProperty("tempoReferencia");
        Assert.Equal("Estimado", depois.GetProperty("origem").GetString());
        Assert.Equal(Math.Max(1, estimativa), depois.GetProperty("minutos").GetInt32());
        Assert.Equal(JsonValueKind.Null, depois.GetProperty("informadoMinutos").ValueKind);

        await NoBancoAsync(async db =>
        {
            var tipo = await db.TiposAto.AsNoTracking().SingleAsync(t => t.Id == tipoId);
            Assert.Equal(1.35m, tipo.PesoComplexidade);
            Assert.Null(tipo.TempoReferenciaMinutos);
        });
    }

    [Theory]
    [InlineData("peso", "{\"peso\":3}", "entre 0,50 e 2,50")]
    [InlineData("peso", "{\"peso\":1.33}", "0,05")]
    [InlineData("tempo-referencia", "{\"minutos\":241}", "entre 2 e 240")]
    [InlineData("tempo-referencia", "{\"minutos\":1}", "entre 2 e 240")]
    public async Task ValorInvalido_400ComMotivo(string rota, string corpo, string trechoDoMotivo)
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);
        var criado = await admin.PostAsJsonAsync("/tipos-ato", new { nome = "Tipo Invalido E2E" });
        var tipoId = (await criado.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tipoAtoId").GetGuid();

        var resposta = await admin.PutAsync($"/tipos-ato/{tipoId}/{rota}", new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Contains(trechoDoMotivo, (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("motivo").GetString());
    }

    [Fact]
    public async Task TipoInexistente_404()
    {
        var admin = await AutenticarComoAsync(Papel.Administrador);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/tipos-ato/{Guid.NewGuid()}/tempo-referencia", new { minutos = 20 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/tipos-ato/{Guid.NewGuid()}/peso", new { peso = 1.25m })).StatusCode);
    }

    [Fact]
    public async Task CheckDoBanco_RecusaPesoForaDaRegra_MesmoPorForaDoDominio()
    {
        await NoBancoAsync(async db =>
        {
            var erro = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO tipos_ato (id, nome, ativo, peso_complexidade) VALUES ({0}, 'Fora da regra', true, 1.33)", Guid.NewGuid()));
            Assert.Equal("ck_tipos_ato_peso_complexidade", erro.ConstraintName);
        });
    }

    // A conversão de dado da migration, contra Postgres de verdade: um banco à parte no mesmo container,
    // migrado até a versão anterior, com pesos inteiros semeados no schema antigo, e então a migration nova
    // (e o Down de volta).
    [Fact]
    public async Task Migration_ConvertePesosInteirosPeloMapaDoDono_EDownVolta()
    {
        var nomeDoBanco = $"conversao_peso_{Guid.NewGuid():N}";
        await using (var admin = new NpgsqlConnection(Fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var criar = new NpgsqlCommand($"CREATE DATABASE {nomeDoBanco}", admin);
            await criar.ExecuteNonQueryAsync();
        }

        var conexao = new NpgsqlConnectionStringBuilder(Fixture.ConnectionString) { Database = nomeDoBanco, Pooling = false }.ConnectionString;
        var opcoes = new DbContextOptionsBuilder<DispatchDbContext>().UseNpgsql(conexao).UseSnakeCaseNamingConvention().Options;
        try
        {
            await using var db = new DispatchDbContext(opcoes);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(MigrationAnterior);

            var pesosAntigos = new[] { 1, 2, 3, 4, 5, 0, 9 };
            foreach (var peso in pesosAntigos)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO tipos_ato (id, nome, ativo, peso_complexidade) VALUES ({0}, {1}, true, {2})",
                    Guid.NewGuid(), $"Tipo {peso}", peso);
            }

            await migrator.MigrateAsync();

            var convertidos = await db.Database
                .SqlQueryRaw<LinhaConvertida>("SELECT nome AS \"Nome\", peso_complexidade AS \"Peso\", tempo_referencia_minutos AS \"Tempo\" FROM tipos_ato")
                .ToDictionaryAsync(l => l.Nome);
            Assert.Equal(1.00m, convertidos["Tipo 1"].Peso);
            Assert.Equal(1.25m, convertidos["Tipo 2"].Peso);
            Assert.Equal(1.50m, convertidos["Tipo 3"].Peso);
            Assert.Equal(1.75m, convertidos["Tipo 4"].Peso);
            Assert.Equal(2.00m, convertidos["Tipo 5"].Peso);
            Assert.Equal(0.50m, convertidos["Tipo 0"].Peso);
            Assert.Equal(2.50m, convertidos["Tipo 9"].Peso);
            Assert.All(convertidos.Values, l => Assert.Null(l.Tempo));
            // E o EF lê de volta pela entidade (constructor binding com o decimal validado).
            Assert.Equal(1.50m, (await db.TiposAto.AsNoTracking().SingleAsync(t => t.Nome == "Tipo 3")).PesoComplexidade);

            await migrator.MigrateAsync(MigrationAnterior);
            var voltaram = await db.Database
                .SqlQueryRaw<LinhaRevertida>("SELECT nome AS \"Nome\", peso_complexidade AS \"Peso\" FROM tipos_ato")
                .ToDictionaryAsync(l => l.Nome, l => l.Peso);
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 1, 5 }, pesosAntigos.Select(p => voltaram[$"Tipo {p}"]));
        }
        finally
        {
            await using var admin = new NpgsqlConnection(Fixture.ConnectionString);
            await admin.OpenAsync();
            await using var dropar = new NpgsqlCommand($"DROP DATABASE IF EXISTS {nomeDoBanco} WITH (FORCE)", admin);
            await dropar.ExecuteNonQueryAsync();
        }
    }

    private sealed record LinhaConvertida(string Nome, decimal Peso, int? Tempo);

    private sealed record LinhaRevertida(string Nome, int Peso);
}
