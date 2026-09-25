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

    // "Fim do dia" é o fim do dia em Brasília (meia-noite local = 03h UTC), não a meia-noite UTC
    // (que é 21h em Brasília e venceria o D+0 três horas antes).
    [Fact]
    public void D0_VenceNoInicioDoDiaSeguinteQuandoEhDiaUtil()
    {
        var prazo = new Prazo(TipoPrazo.D0);

        var esperado = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.FromHours(-3)); // quinta 00h Brasília
        Assert.Equal(esperado, prazo.CalcularVencimento(QuartaFeira));
    }

    // Entre 21h e 24h de Brasília o dia UTC já virou: quarta 22h em Brasília (quinta 01h UTC) ainda
    // é quarta, então o D+0 vence na meia-noite de quinta em Brasília — não na de sexta.
    [Fact]
    public void D0_ReferenciaEntre21hE24hDeBrasilia_VenceNaMeiaNoiteLocalDoMesmoDia()
    {
        var quarta22hBrasilia = new DateTimeOffset(2026, 8, 27, 1, 0, 0, TimeSpan.Zero);
        var prazo = new Prazo(TipoPrazo.D0);

        var vencimento = prazo.CalcularVencimento(quarta22hBrasilia);

        Assert.Equal(new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.FromHours(-3)), vencimento);
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
    // útil, empurra pra segunda 00h (de Brasília).
    [Fact]
    public void D0_Sexta_EmpurraDeSabadoParaSegunda()
    {
        var prazo = new Prazo(TipoPrazo.D0);

        var vencimento = prazo.CalcularVencimento(SextaFeira16h);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(-3)), vencimento); // segunda 00h Brasília
    }

    // Sexta 22h em Brasília já é sábado em UTC — o D+0 ainda é o da sexta (vence sábado 00h
    // local, empurra pra segunda 00h local), e não "domingo 21h" como saía pelo dia UTC.
    [Fact]
    public void D0_SextaAs22hDeBrasilia_EmpurraParaSegundaMeiaNoiteLocal()
    {
        var sexta22hBrasilia = new DateTimeOffset(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);
        var prazo = new Prazo(TipoPrazo.D0);

        var vencimento = prazo.CalcularVencimento(sexta22hBrasilia);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.FromHours(-3)), vencimento);
    }

    // O dia da semana do ajuste de dia útil é o de Brasília: sexta 22h local + 24h = sábado 22h
    // local (domingo 01h UTC) — empurra 2 dias, pra segunda 22h local. Pelo dia UTC empurrava 1
    // e vencia no domingo 22h.
    [Fact]
    public void D1_SextaAs22hDeBrasilia_EmpurraDeSabadoParaSegundaNoHorarioLocal()
    {
        var sexta22hBrasilia = new DateTimeOffset(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);
        var prazo = new Prazo(TipoPrazo.D1);

        var vencimento = prazo.CalcularVencimento(sexta22hBrasilia);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 22, 0, 0, TimeSpan.FromHours(-3)), vencimento);
    }

    // Quinta 22h local + 24h = sexta 22h local, dia útil — mesmo sendo sábado 01h em UTC, não empurra.
    [Fact]
    public void D1_QuintaAs22hDeBrasilia_VenceNaSextaSemEmpurrar()
    {
        var quinta22hBrasilia = new DateTimeOffset(2026, 8, 28, 1, 0, 0, TimeSpan.Zero);
        var prazo = new Prazo(TipoPrazo.D1);

        var vencimento = prazo.CalcularVencimento(quinta22hBrasilia);

        Assert.Equal(quinta22hBrasilia.AddHours(24), vencimento);
    }

    // Pedido do dono ("equipe X entra na etapa Y depois das 16h, vence às 10h do dia
    // seguinte") — CorteDeHorario sempre vence no dia seguinte (horário de Brasília) ao
    // HorarioDeVencimento configurado; quem decide "está depois do corte" é Equipe.PrazoPara,
    // não Prazo — aqui só se testa "dado que já é CorteDeHorario, o vencimento é o certo".
    [Fact]
    public void CorteDeHorario_VenceNoHorarioConfiguradoDoDiaSeguinte()
    {
        // QuartaFeira = 26/08 14:30 UTC = 26/08 11:30 em Brasília (ainda quarta).
        var prazo = new Prazo(TipoPrazo.CorteDeHorario, new TimeOnly(10, 0));

        var vencimento = prazo.CalcularVencimento(QuartaFeira);

        Assert.Equal(new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.FromHours(-3)), vencimento); // quinta 10h Brasília
    }

    // Mesmo ajuste de dia útil que D0/D1/D2 já usam: se o dia seguinte cair num fim de semana,
    // empurra pro próximo dia útil, no mesmo horário configurado.
    [Fact]
    public void CorteDeHorario_DiaSeguinteCaiNoFimDeSemana_EmpurraParaSegunda()
    {
        // SextaFeira16h = 28/08 16h UTC = 28/08 13h em Brasília (ainda sexta) — dia seguinte
        // (sábado 29/08) não é dia útil, empurra pra segunda 31/08.
        var prazo = new Prazo(TipoPrazo.CorteDeHorario, new TimeOnly(10, 0));

        var vencimento = prazo.CalcularVencimento(SextaFeira16h);

        Assert.Equal(new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.FromHours(-3)), vencimento);
        Assert.Equal(DayOfWeek.Monday, vencimento.DayOfWeek);
    }
}
