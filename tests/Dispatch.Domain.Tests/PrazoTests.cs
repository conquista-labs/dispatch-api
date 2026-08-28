namespace Dispatch.Domain.Tests;

public class PrazoTests
{
    // Quarta-feira — nenhum dos prazos cruza fim de semana a partir daqui, serve de linha de
    // base sem o ajuste de dia útil interferindo.
    private static readonly DateTimeOffset QuartaFeira = new(2026, 8, 26, 14, 30, 0, TimeSpan.Zero);

    // Sexta-feira 16h — usado pra testar o ajuste de dia útil (RF confirmado com a operação):
    // D+1 (24h) cairia no sábado, D+2 (48h) cairia no domingo.
    private static readonly DateTimeOffset SextaFeira16h = new(2026, 8, 28, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UmaHora_VenceUmaHoraAposAReferencia()
    {
        var prazo = new Prazo(TipoPrazo.UmaHora);

        Assert.Equal(QuartaFeira.AddHours(1), prazo.CalcularVencimento(QuartaFeira));
    }

    // "1 hora" é o prazo mais urgente do sistema (RF-13) — não pode ser empurrado pra depois
    // do fim de semana, mesmo que a referência seja sexta à noite.
    [Fact]
    public void UmaHora_NaoEmpurraParaDiaUtilMesmoCruzandoFimDeSemana()
    {
        var referencia = new DateTimeOffset(2026, 8, 28, 23, 30, 0, TimeSpan.Zero); // sexta 23h30
        var prazo = new Prazo(TipoPrazo.UmaHora);

        var vencimento = prazo.CalcularVencimento(referencia);

        Assert.Equal(referencia.AddHours(1), vencimento); // sábado 00h30, sem ajuste
    }

    [Fact]
    public void D0_VenceNoInicioDoDiaSeguinteQuandoEhDiaUtil()
    {
        var prazo = new Prazo(TipoPrazo.D0);

        var esperado = new DateTimeOffset(QuartaFeira.Date, QuartaFeira.Offset).AddDays(1); // quinta 00h
        Assert.Equal(esperado, prazo.CalcularVencimento(QuartaFeira));
    }

    [Fact]
    public void D1_VenceExatamente24HorasDepoisQuandoNaoCruzaFimDeSemana()
    {
        var prazo = new Prazo(TipoPrazo.D1);

        Assert.Equal(QuartaFeira.AddHours(24), prazo.CalcularVencimento(QuartaFeira));
    }

    [Fact]
    public void D2_VenceExatamente48HorasDepoisQuandoNaoCruzaFimDeSemana()
    {
        var prazo = new Prazo(TipoPrazo.D2);

        Assert.Equal(QuartaFeira.AddHours(48), prazo.CalcularVencimento(QuartaFeira));
    }

    // Exemplo confirmado com a operação: sexta 16h + D+1 (24h) cairia em sábado 16h — não é
    // dia útil, empurra pro próximo dia útil (segunda) no mesmo horário.
    [Fact]
    public void D1_SextaAsDezesseisHoras_EmpurraDeSabadoParaSegunda()
    {
        var prazo = new Prazo(TipoPrazo.D1);

        var vencimento = prazo.CalcularVencimento(SextaFeira16h);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 16, 0, 0, TimeSpan.Zero), vencimento); // segunda 16h
        Assert.Equal(DayOfWeek.Monday, vencimento.DayOfWeek);
    }

    // Mesmo exemplo: sexta 16h + D+2 (48h) cairia em domingo 16h — também não é dia útil,
    // empurra pra segunda. Coincide com o resultado do D+1 nesse caso específico (esperado,
    // não é bug: os dois "pulam" o mesmo fim de semana).
    [Fact]
    public void D2_SextaAsDezesseisHoras_EmpurraDeDomingoParaSegunda()
    {
        var prazo = new Prazo(TipoPrazo.D2);

        var vencimento = prazo.CalcularVencimento(SextaFeira16h);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 16, 0, 0, TimeSpan.Zero), vencimento); // segunda 16h
        Assert.Equal(DayOfWeek.Monday, vencimento.DayOfWeek);
    }

    // D+0 de uma sexta vence no início do sábado (fim do dia de sexta) — sábado não é dia
    // útil, empurra pra segunda 00h.
    [Fact]
    public void D0_Sexta_EmpurraDeSabadoParaSegunda()
    {
        var prazo = new Prazo(TipoPrazo.D0);

        var vencimento = prazo.CalcularVencimento(SextaFeira16h);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero), vencimento); // segunda 00h
        Assert.Equal(DayOfWeek.Monday, vencimento.DayOfWeek);
    }
}
