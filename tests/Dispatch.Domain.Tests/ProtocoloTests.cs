namespace Dispatch.Domain.Tests;

public class ProtocoloTests
{
    private static Protocolo NovoProtocolo(Prioridade prioridade = Prioridade.Normal) =>
        new(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow, prioridade);

    [Theory]
    [InlineData(TipoPrazo.UmaHora)]
    [InlineData(TipoPrazo.D0)]
    public void PrazoUmaHoraOuD0_TornaOProtocoloUrgente(TipoPrazo tipo)
    {
        var protocolo = NovoProtocolo();

        protocolo.DefinirPrazo(new Prazo(tipo), DateTimeOffset.UtcNow);

        Assert.True(protocolo.Urgente);
    }

    [Theory]
    [InlineData(TipoPrazo.D1)]
    [InlineData(TipoPrazo.D2)]
    public void PrazoD1OuD2ComPrioridadeNormal_NaoEhUrgente(TipoPrazo tipo)
    {
        var protocolo = NovoProtocolo();

        protocolo.DefinirPrazo(new Prazo(tipo), DateTimeOffset.UtcNow);

        Assert.False(protocolo.Urgente);
    }

    [Fact]
    public void PrioridadeAlta_EhUrgenteMesmoComPrazoD2()
    {
        var protocolo = NovoProtocolo(Prioridade.Alta);

        protocolo.DefinirPrazo(new Prazo(TipoPrazo.D2), DateTimeOffset.UtcNow);

        Assert.True(protocolo.Urgente);
    }

    [Fact]
    public void SemPrazoDefinido_UrgenciaDependeSoDaPrioridade()
    {
        var protocolo = NovoProtocolo(Prioridade.Alta);

        Assert.True(protocolo.Urgente);
    }

    [Fact]
    public void DefinirPrazo_CalculaEArmazenaOVencimento()
    {
        var protocolo = NovoProtocolo();
        var referencia = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

        protocolo.DefinirPrazo(new Prazo(TipoPrazo.UmaHora), referencia);

        Assert.Equal(referencia.AddHours(1), protocolo.VencimentoEm);
    }
}
