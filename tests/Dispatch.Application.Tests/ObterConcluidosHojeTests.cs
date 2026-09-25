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

    // "Hoje" é o dia de Brasília: às 23h de Brasília (02h UTC do dia seguinte) o que foi
    // concluído às 20h de Brasília (23h UTC) ainda é de hoje. Pelo dia UTC, o dia tinha
    // "começado" às 21h de Brasília e o das 20h sumia; o das 23h da véspera não pode entrar.
    [Fact]
    public async Task Entre21hE24hDeBrasilia_HojeEhODiaLocalNaoODiaUtc()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var brasilia = TimeSpan.FromHours(-3);
        var agora = new DateTimeOffset(2026, 8, 27, 23, 0, 0, brasilia).ToUniversalTime(); // 28/08 02h UTC

        var as20h = ConcluidoPor(conferente, new DateTimeOffset(2026, 8, 27, 20, 0, 0, brasilia));
        var as22h = ConcluidoPor(conferente, new DateTimeOffset(2026, 8, 27, 22, 0, 0, brasilia));
        var vespera23h = ConcluidoPor(conferente, new DateTimeOffset(2026, 8, 26, 23, 0, 0, brasilia));

        var casoDeUso = new ObterConcluidosHoje(
            new FakeProtocoloRepository([as20h, as22h, vespera23h]), new FakeRelogio(agora));

        var concluidos = await casoDeUso.ExecutarAsync(conferente);

        Assert.Equal(new[] { as20h.Id, as22h.Id }.Order(), concluidos.Select(p => p.Id).Order());
    }

    private static Protocolo ConcluidoPor(Conferente conferente, DateTimeOffset concluidoEm)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, concluidoEm.AddHours(-2));
        protocolo.AtribuirA(conferente.Id, concluidoEm.AddHours(-2));
        protocolo.IniciarConferencia(concluidoEm.AddHours(-1));
        protocolo.Aprovar(concluidoEm.ToUniversalTime());
        return protocolo;
    }
}
