namespace Dispatch.Domain.Tests;

// RF-46a/b: ritmo = Σ tempo real ÷ Σ tempo de referência dos atos elegíveis (1,00× = a referência;
// abaixo, mais rápido). Ato sem tipo (sem referência) ou sem tempo medido fica fora; sem nenhum ato
// elegível o ritmo é nulo (não 0×, que diria "infinitamente rápido").
public class RitmoTests
{
    private static AtoParaRitmo Ato(double? minutosReais, int? referenciaMinutos) =>
        new(minutosReais is { } m ? TimeSpan.FromMinutes(m) : null, referenciaMinutos);

    [Fact]
    public void Ritmo_SomaRealSobreSomaReferencia_NaoMediaDeRazoes()
    {
        // 10/20 = 0,5 e 30/20 = 1,5 → média das razões seria 1,0; Σ/Σ = 40/40 = 1,0 também. Caso que
        // separa: 10/10 = 1 e 40/80 = 0,5 → média 0,75; Σ/Σ = 50/90.
        var ritmo = Ritmo.Calcular([Ato(10, 10), Ato(40, 80)]);

        Assert.NotNull(ritmo);
        Assert.Equal(50.0 / 90, ritmo.Valor, 10);
        Assert.Equal(2, ritmo.Atos);
        Assert.Equal(TimeSpan.FromMinutes(45), ritmo.TempoMedioReferencia);
    }

    [Fact]
    public void ExemploDoRequisito_16MinBrutoContraReferencia21_Da076()
    {
        var ritmo = Ritmo.Calcular([Ato(16, 21)]);

        Assert.Equal(0.76, Math.Round(ritmo!.Valor, 2));
    }

    [Fact]
    public void AtoSemTipo_FicaForaDoRitmo()
    {
        var ritmo = Ritmo.Calcular([Ato(10, 20), Ato(500, null)]);

        Assert.Equal(0.5, ritmo!.Valor, 10);
        Assert.Equal(1, ritmo.Atos);
    }

    [Fact]
    public void AtoSemTempoMedido_FicaForaDoRitmo()
    {
        var ritmo = Ritmo.Calcular([Ato(10, 20), Ato(null, 60)]);

        Assert.Equal(0.5, ritmo!.Valor, 10);
        Assert.Equal(TimeSpan.FromMinutes(20), ritmo.TempoMedioReferencia);
    }

    [Fact]
    public void SemAtoElegivel_EhNulo()
    {
        Assert.Null(Ritmo.Calcular([]));
        Assert.Null(Ritmo.Calcular([Ato(10, null), Ato(null, 15)]));
    }

    [Fact]
    public void MediaSimples_IgnoraQuemNaoTemRitmo()
    {
        Assert.Equal(0.8, Ritmo.MediaSimples([0.6, null, 1.0])!.Value, 10);
        Assert.Null(Ritmo.MediaSimples([null, null]));
    }
}
