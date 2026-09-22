using System.Net;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Tests;

[Collection(IntegracaoCollection.Nome)]
public sealed class SchemaTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    [Fact]
    public async Task TodasAsMigrationsAplicamDoZeroSemPendencia()
    {
        // O Migrate() roda no fixture, contra um Postgres vazio. Se qualquer migration quebrar
        // nesse cenário (o mesmo de um banco novo em produção), a suíte inteira nem começa.
        await NoBancoAsync(async db => Assert.Empty(await db.Database.GetPendingMigrationsAsync()));
    }

    [Fact]
    public async Task LinhaDeConfiguracaoNasceSemeadaPelaMigration()
    {
        // ConfiguracaoRepository.ObterAsync usa SingleAsync — sem a linha semeada pela migration
        // AdicionaConfiguracao, todo endpoint que lê as faixas do semáforo quebraria em runtime.
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);

        var resposta = await cliente.GetAsync("/config");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        await NoBancoAsync(async db => Assert.Equal(1, await db.Configuracoes.CountAsync()));
    }
}
