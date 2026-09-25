namespace Dispatch.Domain.Tests;

// ADR-0032/0035: o tempo de um ato é repartido por ciclo (cada ciclo na conta de quem o fez); um
// ajuste manual substitui tudo e vai inteiro pro dono atual. O ritmo (RF-46a) usa o tempo do dono
// atual no ato; a mediana do tempo de referência (RF-46c) usa a Duracao inteira.
public class TempoPorConferenteTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Protocolo Novo() =>
        new(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, Inicio);

    [Fact]
    public void AtoReabertoEReatribuido_RepartePorCiclo()
    {
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        var protocolo = Novo();
        protocolo.AtribuirA(ana, Inicio);
        protocolo.IniciarConferencia(Inicio);
        protocolo.Aprovar(Inicio.AddMinutes(20));
        protocolo.ReabrirConferencia(Inicio.AddHours(1));
        protocolo.AtribuirA(bruno, Inicio.AddHours(1));
        protocolo.IniciarConferencia(Inicio.AddHours(1));
        protocolo.Aprovar(Inicio.AddHours(1).AddMinutes(5));

        var tempos = protocolo.TemposPorConferente();

        Assert.Equal([(ana, TimeSpan.FromMinutes(20)), (bruno, TimeSpan.FromMinutes(5))], tempos);
        Assert.Equal(TimeSpan.FromMinutes(5), protocolo.TempoDe(bruno));
        Assert.Equal(TimeSpan.FromMinutes(20), protocolo.TempoDe(ana));
        Assert.Null(protocolo.TempoDe(Guid.NewGuid()));
        Assert.Equal(TimeSpan.FromMinutes(25), protocolo.Duracao);
    }

    [Fact]
    public void Pausa_SomaOsDoisPedacosNaContaDoMesmoDono()
    {
        var ana = Guid.NewGuid();
        var protocolo = Novo();
        protocolo.AtribuirA(ana, Inicio);
        protocolo.IniciarConferencia(Inicio);
        protocolo.Pausar(Inicio.AddMinutes(10));
        protocolo.Retomar(Inicio.AddMinutes(70));
        protocolo.Aprovar(Inicio.AddMinutes(75));

        Assert.Equal(TimeSpan.FromMinutes(15), protocolo.TempoDe(ana));
        Assert.Equal(TimeSpan.FromMinutes(15), protocolo.Duracao);
    }

    [Fact]
    public void AjusteManual_VaiInteiroProDonoAtual()
    {
        var ana = Guid.NewGuid();
        var bruno = Guid.NewGuid();
        var protocolo = Novo();
        protocolo.AtribuirA(ana, Inicio);
        protocolo.IniciarConferencia(Inicio);
        protocolo.Aprovar(Inicio.AddMinutes(20));
        protocolo.ReabrirConferencia(Inicio.AddHours(1));
        protocolo.AtribuirA(bruno, Inicio.AddHours(1));
        protocolo.IniciarConferencia(Inicio.AddHours(1));
        protocolo.Aprovar(Inicio.AddHours(1).AddMinutes(5));
        protocolo.AjustarDuracao(TimeSpan.FromMinutes(12), Guid.NewGuid(), Inicio.AddHours(2), "esqueceu aberto");

        Assert.Equal([(bruno, TimeSpan.FromMinutes(12))], protocolo.TemposPorConferente());
        Assert.Null(protocolo.TempoDe(ana));
    }

    [Fact]
    public void CalcularDuracao_EhAMesmaContaDaPropriedade()
    {
        // A projeção do histórico (Infrastructure) usa esta função sem carregar o Protocolo inteiro.
        Assert.Equal(
            TimeSpan.FromMinutes(25),
            Protocolo.CalcularDuracao(null, Inicio, Inicio.AddMinutes(5), [TimeSpan.FromMinutes(20)]));
        Assert.Equal(
            TimeSpan.FromMinutes(3),
            Protocolo.CalcularDuracao(TimeSpan.FromMinutes(3), Inicio, Inicio.AddMinutes(5), [TimeSpan.FromMinutes(20)]));
        Assert.Null(Protocolo.CalcularDuracao(null, null, Inicio, []));
    }
}
