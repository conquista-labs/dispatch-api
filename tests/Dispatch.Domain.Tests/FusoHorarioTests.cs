namespace Dispatch.Domain.Tests;

// "Hoje" é o dia de Brasília, não o dia UTC: entre 21h e 24h de Brasília o relógio UTC já virou
// o dia seguinte, e calcular o início do dia pelo UtcNow.Date zerava os "concluídos hoje" às 21h.
public class FusoHorarioTests
{
    private static readonly DateTimeOffset InicioDe26DeAgostoEmBrasilia = new(2026, 8, 26, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public void InicioDoDiaLocal_DuasDaManhaUtc_EhOInicioDaVesperaEmBrasilia()
    {
        var duasDaManhaUtc = new DateTimeOffset(2026, 8, 27, 2, 0, 0, TimeSpan.Zero); // 26/08 23h em Brasília

        Assert.Equal(InicioDe26DeAgostoEmBrasilia, FusoHorario.InicioDoDiaLocal(duasDaManhaUtc));
    }

    [Fact]
    public void InicioDoDiaLocal_MeioDoDia_EhAsTresDaManhaUtcDoMesmoDia()
    {
        var meioDoDia = new DateTimeOffset(2026, 8, 26, 15, 0, 0, TimeSpan.Zero); // 12h em Brasília

        Assert.Equal(InicioDe26DeAgostoEmBrasilia, FusoHorario.InicioDoDiaLocal(meioDoDia));
    }

    // Borda: meia-noite de Brasília em ponto já é o dia novo; um segundo antes ainda é a véspera.
    [Fact]
    public void InicioDoDiaLocal_MeiaNoiteDeBrasiliaEmPonto_JaEhODiaNovo()
    {
        var meiaNoite = new DateTimeOffset(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

        Assert.Equal(meiaNoite, FusoHorario.InicioDoDiaLocal(meiaNoite));
        Assert.Equal(InicioDe26DeAgostoEmBrasilia, FusoHorario.InicioDoDiaLocal(meiaNoite.AddSeconds(-1)));
    }

    // O resultado volta em UTC (offset 0) — o Npgsql recusa offset diferente de zero, e o
    // instante vira parâmetro de query nos repositórios. A entrada pode vir em qualquer offset.
    [Fact]
    public void InicioDoDiaLocal_DevolveEmUtcIndependenteDoOffsetDaEntrada()
    {
        var emBrasilia = new DateTimeOffset(2026, 8, 26, 23, 0, 0, TimeSpan.FromHours(-3));

        var inicio = FusoHorario.InicioDoDiaLocal(emBrasilia);

        Assert.Equal(InicioDe26DeAgostoEmBrasilia, inicio);
        Assert.Equal(TimeSpan.Zero, inicio.Offset);
    }

    [Fact]
    public void DiaLocal_DuasDaManhaUtc_EhAVesperaEmBrasilia()
    {
        Assert.Equal(new DateOnly(2026, 8, 26), FusoHorario.DiaLocal(new DateTimeOffset(2026, 8, 27, 2, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateOnly(2026, 8, 27), FusoHorario.DiaLocal(new DateTimeOffset(2026, 8, 27, 3, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void InicioDoDia_DeUmDiaLocal_EhAsTresDaManhaUtc()
    {
        var inicio = FusoHorario.InicioDoDia(new DateOnly(2026, 8, 26));

        Assert.Equal(InicioDe26DeAgostoEmBrasilia, inicio);
        Assert.Equal(TimeSpan.Zero, inicio.Offset);
    }
}
