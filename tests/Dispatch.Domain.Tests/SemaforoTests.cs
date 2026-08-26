namespace Dispatch.Domain.Tests;

public class SemaforoTests
{
    private static readonly TimeSpan FaixaAtencao = TimeSpan.FromHours(4);
    private static readonly TimeSpan FaixaUrgente = TimeSpan.FromMinutes(60);
    private static readonly DateTimeOffset Agora = new(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FaltaMaisQueAFaixaDeAtencao_Verde()
    {
        var faixa = Semaforo.Calcular(Agora.AddHours(5), Agora, FaixaAtencao, FaixaUrgente);

        Assert.Equal(FaixaSemaforo.Verde, faixa);
    }

    [Fact]
    public void FaltaMenosDeQuatroHoras_Amarelo()
    {
        var faixa = Semaforo.Calcular(Agora.AddHours(3), Agora, FaixaAtencao, FaixaUrgente);

        Assert.Equal(FaixaSemaforo.Amarelo, faixa);
    }

    [Fact]
    public void FaltaMenosDeSessentaMinutos_Laranja()
    {
        var faixa = Semaforo.Calcular(Agora.AddMinutes(30), Agora, FaixaAtencao, FaixaUrgente);

        Assert.Equal(FaixaSemaforo.Laranja, faixa);
    }

    [Fact]
    public void VencimentoNoPassado_Vermelho()
    {
        var faixa = Semaforo.Calcular(Agora.AddHours(-2), Agora, FaixaAtencao, FaixaUrgente);

        Assert.Equal(FaixaSemaforo.Vermelho, faixa);
    }
}
