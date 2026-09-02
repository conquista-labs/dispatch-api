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

    [Theory]
    [InlineData(TipoPrazo.D1)]
    [InlineData(TipoPrazo.D2)]
    public void PrazoD1OuD2ComPrioridadeBaixa_NaoEhUrgente(TipoPrazo tipo)
    {
        var protocolo = NovoProtocolo(Prioridade.Baixa);

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

    [Fact]
    public void CorrigirResultado_Aprovado_ViraReprovado()
    {
        var protocolo = NovoProtocolo();
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow);

        var agora = DateTimeOffset.UtcNow.AddMinutes(5);
        protocolo.CorrigirResultado(agora);

        Assert.Equal(StatusProtocolo.Reprovado, protocolo.Status);
        Assert.Equal(agora, protocolo.CorrigidoEm);
    }

    [Fact]
    public void CorrigirResultado_Reprovado_ViraAprovado()
    {
        var protocolo = NovoProtocolo();
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Reprovar(DateTimeOffset.UtcNow);

        protocolo.CorrigirResultado(DateTimeOffset.UtcNow);

        Assert.Equal(StatusProtocolo.Aprovado, protocolo.Status);
    }

    [Fact]
    public void CorrigirResultado_PermiteCorrigirMaisDeUmaVez()
    {
        var protocolo = NovoProtocolo();
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        protocolo.Aprovar(DateTimeOffset.UtcNow);

        protocolo.CorrigirResultado(DateTimeOffset.UtcNow);
        protocolo.CorrigirResultado(DateTimeOffset.UtcNow);

        Assert.Equal(StatusProtocolo.Aprovado, protocolo.Status);
    }

    [Fact]
    public void ReabrirConferencia_VoltaPraConferindoComCronometroDoZero()
    {
        var protocolo = NovoProtocolo();
        var inicioOriginal = DateTimeOffset.UtcNow;
        protocolo.IniciarConferencia(inicioOriginal);
        protocolo.Aprovar(inicioOriginal.AddMinutes(10));

        var agora = inicioOriginal.AddHours(2);
        protocolo.ReabrirConferencia(agora);

        Assert.Equal(StatusProtocolo.Conferindo, protocolo.Status);
        Assert.Equal(agora, protocolo.IniciadoEm);
        Assert.Null(protocolo.ConcluidoEm);
        Assert.Null(protocolo.Duracao);
        Assert.Equal(agora, protocolo.ReabertoEm);
    }

    [Fact]
    public void Excluir_GuardaOStatusAnteriorEViraExcluido()
    {
        var protocolo = NovoProtocolo();
        protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);

        protocolo.Excluir();

        Assert.Equal(StatusProtocolo.Excluido, protocolo.Status);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.StatusAntesDeExcluir);
    }

    [Fact]
    public void Restaurar_DevolveOStatusAnteriorELimpaOCampo()
    {
        var protocolo = NovoProtocolo();
        var donoId = Guid.NewGuid();
        protocolo.AtribuirA(donoId, DateTimeOffset.UtcNow);
        var vencimentoOriginal = protocolo.VencimentoEm;
        protocolo.Excluir();

        protocolo.Restaurar();

        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Null(protocolo.StatusAntesDeExcluir);
        Assert.Equal(donoId, protocolo.DonoId);
        Assert.Equal(vencimentoOriginal, protocolo.VencimentoEm);
    }

    [Fact]
    public void EditarDadosBasicos_TrocaTipoEscreventeEEtapa()
    {
        var protocolo = NovoProtocolo();
        var novoTipoAtoId = Guid.NewGuid();
        var novoEscreventeId = Guid.NewGuid();

        protocolo.EditarDadosBasicos(novoTipoAtoId, novoEscreventeId, Etapa.PosConferencia);

        Assert.Equal(novoTipoAtoId, protocolo.TipoAtoId);
        Assert.Equal(novoEscreventeId, protocolo.EscreventeId);
        Assert.Equal(Etapa.PosConferencia, protocolo.Etapa);
    }
}
