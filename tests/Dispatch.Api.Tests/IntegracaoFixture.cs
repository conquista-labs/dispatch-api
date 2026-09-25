using Dispatch.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace Dispatch.Api.Tests;

// Um Postgres efêmero (Testcontainers) por execução da suíte, compartilhado por todas as
// classes de teste — subir container é a parte cara, não vale repetir por teste. O Postgres do
// docker-compose.yml de propósito NÃO é usado: ele acumula dado de sessão de desenvolvimento
// (e já teve clone de produção), então teste que dependesse dele seria flaky por natureza.
//
// O Migrate() daqui é, de graça, o teste de fumaça do schema: roda todas as migrations do zero
// contra um banco vazio, que é exatamente o cenário em que uma migration mal feita quebra
// (ex.: CHECK novo sem backfill da linha existente — já aconteceu neste projeto, Motor v2).
public sealed class IntegracaoFixture : IAsyncLifetime
{
    // Mesma major do Postgres do docker-compose local (17) — o Neon roda 18 em produção, mas
    // nada aqui depende de recurso exclusivo de uma das duas.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    private NpgsqlConnection _conexaoDeReset = null!;
    private Respawner _respawner = null!;

    public DispatchApiFactory Factory { get; private set; } = null!;

    // Pra teste que precisa de um banco à parte no mesmo container (ex.: migrar até uma versão antiga,
    // semear dado no schema de antes e subir a migration de conversão).
    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Factory = new DispatchApiFactory(_postgres.GetConnectionString());

        using (var escopo = Factory.Services.CreateScope())
        {
            await escopo.ServiceProvider.GetRequiredService<DispatchDbContext>().Database.MigrateAsync();
        }

        _conexaoDeReset = new NpgsqlConnection(_postgres.GetConnectionString());
        await _conexaoDeReset.OpenAsync();
        _respawner = await Respawner.CreateAsync(_conexaoDeReset, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = ["public"],
            // `configuracao` é semeada pela própria migration (linha única, sem ela
            // ConfiguracaoRepository.ObterAsync quebra com SingleAsync) — truncar seria apagar
            // parte do schema, não dado de teste. `__EFMigrationsHistory` pelo mesmo motivo.
            TablesToIgnore = [new Table("public", "__EFMigrationsHistory"), new Table("public", "configuracao")],
        });
    }

    // Respawn trunca respeitando a ordem de FK sozinho — cada teste começa do mesmo estado sem
    // depender do que o teste anterior deixou para trás.
    public Task ResetarBancoAsync() => _respawner.ResetAsync(_conexaoDeReset);

    public async Task DisposeAsync()
    {
        await _conexaoDeReset.DisposeAsync();
        await Factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class IntegracaoCollection : ICollectionFixture<IntegracaoFixture>
{
    public const string Nome = "integracao";
}
