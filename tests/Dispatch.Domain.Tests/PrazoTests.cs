namespace Dispatch.Domain.Tests;

public class PrazoTests
{
    private static readonly DateTimeOffset Referencia = new(2026, 8, 26, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void UmaHora_VenceUmaHoraAposAReferencia()
    {
        var prazo = new Prazo(TipoPrazo.UmaHora);

        var vencimento = prazo.CalcularVencimento(Referencia);

        Assert.Equal(Referencia.AddHours(1), vencimento);
    }

    [Theory]
    [InlineData(TipoPrazo.D0, 0)]
    [InlineData(TipoPrazo.D1, 1)]
    [InlineData(TipoPrazo.D2, 2)]
    public void PrazoPorDias_VenceNoInicioDoDiaSeguinteAoUltimoDiaDoPrazo(TipoPrazo tipo, int diasDePrazo)
    {
        var prazo = new Prazo(tipo);

        var vencimento = prazo.CalcularVencimento(Referencia);

        var esperado = new DateTimeOffset(Referencia.Date, Referencia.Offset).AddDays(diasDePrazo + 1);
        Assert.Equal(esperado, vencimento);
    }
}
