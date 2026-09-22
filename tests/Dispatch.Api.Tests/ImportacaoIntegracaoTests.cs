using System.Net;
using System.Net.Http.Json;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Api.Tests;

[Collection(IntegracaoCollection.Nome)]
public sealed class ImportacaoIntegracaoTests(IntegracaoFixture fixture) : IntegracaoTestBase(fixture)
{
    private static readonly DateTimeOffset Andamento = new(2026, 3, 10, 9, 0, 0, TimeSpan.FromHours(-3));

    // RF-07: a linha de corte é o que evita reprocessar um relatório já importado. Rodar contra
    // Postgres de verdade também cobre o caminho que já quebrou FK uma vez (tipo desconhecido é
    // sinalizado com TipoAtoId nulo, nunca criado no catálogo) e RF-09 (escrevente desconhecido
    // nasce sem equipe) — as duas coisas que fake nenhum consegue provar.
    [Fact]
    public async Task ReimportarComCorteDepoisDoAndamento_NaoDuplicaNada()
    {
        var cliente = await AutenticarComoAsync(Papel.Distribuidora);

        var primeira = await cliente.PostAsJsonAsync("/protocolos/importar/confirmar", Lote(Andamento.AddDays(-1)));
        Assert.True(primeira.IsSuccessStatusCode, await primeira.Content.ReadAsStringAsync());

        await NoBancoAsync(async db =>
        {
            Assert.Equal(2, await db.Protocolos.CountAsync());

            // Tipo de ato e escrevente desconhecidos nascem na própria importação (RF-09) — as
            // duas FKs que já quebraram de verdade uma vez, e que fake nenhum tem como violar.
            var tipoAto = Assert.Single(await db.TiposAto.ToListAsync());
            Assert.True(await db.Protocolos.AllAsync(p => p.TipoAtoId == tipoAto.Id));

            var escrevente = Assert.Single(await db.Escreventes.ToListAsync());
            Assert.Null(escrevente.EquipeId);
            Assert.True(await db.Protocolos.AllAsync(p => p.EscreventeId == escrevente.Id));
        });

        var segunda = await cliente.PostAsJsonAsync("/protocolos/importar/confirmar", Lote(Andamento.AddHours(1)));
        Assert.Equal(HttpStatusCode.OK, segunda.StatusCode);

        await NoBancoAsync(async db => Assert.Equal(2, await db.Protocolos.CountAsync()));
    }

    private static object Lote(DateTimeOffset linhaDeCorte) => new
    {
        etapa = nameof(Etapa.PosConferencia),
        linhaDeCorte,
        linhas = new[]
        {
            new { protocolo = "900001", tipoAto = "Venda e Compra", escrevente = "Ana Souza", dataHoraAndamento = Andamento },
            new { protocolo = "900002", tipoAto = "Venda e Compra", escrevente = "Ana Souza", dataHoraAndamento = Andamento },
        },
    };
}
