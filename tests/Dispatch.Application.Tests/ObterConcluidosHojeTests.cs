using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterConcluidosHojeTests
{
    [Fact]
    public async Task SoTraConcluidosDoConferenteDesdeOInicioDoDia()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var agora = new DateTimeOffset(2026, 8, 27, 15, 30, 0, TimeSpan.Zero);
        var inicioDoDia = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero);

        var concluidoHoje = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        concluidoHoje.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        concluidoHoje.IniciarConferencia(inicioDoDia.AddHours(10));
        concluidoHoje.Aprovar(inicioDoDia.AddHours(11));

        var concluidoOntem = new Protocolo(Guid.NewGuid(), "2", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        concluidoOntem.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        concluidoOntem.IniciarConferencia(inicioDoDia.AddDays(-1));
        concluidoOntem.Reprovar(inicioDoDia.AddDays(-1).AddHours(1));

        var casoDeUso = new ObterConcluidosHoje(
            new FakeProtocoloRepository([concluidoHoje, concluidoOntem]), new FakeRelogio(agora));

        var concluidos = await casoDeUso.ExecutarAsync(conferente);

        var resultado = Assert.Single(concluidos);
        Assert.Equal(concluidoHoje.Id, resultado.Id);
    }
}
