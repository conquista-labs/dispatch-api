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

    // Achado em uso real (produção, protocolo 263605): reabrir bem antes só ligava o
    // cronômetro na hora (Status ia direto pra Conferindo) — o ato pulava pra "Em conferência"
    // sem a pessoa ter clicado em nada. Agora devolve pra Atribuído (fila da pessoa); o
    // cronômetro só liga quando ela chamar IniciarConferencia de novo, igual a primeira vez.
    [Fact]
    public void ReabrirConferencia_VoltaPraAtribuidoSemLigarOCronometro()
    {
        var protocolo = NovoProtocolo();
        var inicioOriginal = DateTimeOffset.UtcNow;
        protocolo.IniciarConferencia(inicioOriginal);
        protocolo.Aprovar(inicioOriginal.AddMinutes(10));

        var agora = inicioOriginal.AddHours(2);
        protocolo.ReabrirConferencia(agora);

        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Null(protocolo.IniciadoEm);
        Assert.Null(protocolo.ConcluidoEm);
        Assert.Null(protocolo.Duracao);
        Assert.Equal(agora, protocolo.ReabertoEm);
    }

    // Achado em uso real (produção): antes deste campo, a Duracao final de um protocolo
    // reaberto só refletia o último ciclo (a reabertura), perdendo o tempo da conferência
    // original — o dono via "5 min" num ato que na verdade ficou muito mais tempo em aberto.
    [Fact]
    public void ReabrirConferenciaEConcluirDeNovo_DuracaoSomaOsDoisCiclos()
    {
        var protocolo = NovoProtocolo();
        var inicioOriginal = DateTimeOffset.UtcNow;
        // ReabrirConferencia só registra o ciclo anterior com um DonoId de verdade (precisa
        // saber de quem foi aquele ciclo, ver CicloConferencia) — reflete o fluxo real, onde
        // IniciarConferencia nunca acontece sem AtribuirA antes.
        protocolo.AtribuirA(Guid.NewGuid(), inicioOriginal);
        protocolo.IniciarConferencia(inicioOriginal);
        protocolo.Reprovar(inicioOriginal.AddMinutes(20)); // 1º ciclo: 20 min

        protocolo.ReabrirConferencia(inicioOriginal.AddHours(2));
        // A distribuidora aprovou o pedido de reabertura às +2h, mas o conferente só retomou
        // de verdade às +3h (fora do horário dele, por exemplo) — esse intervalo de espera não
        // pode contar como tempo de conferência.
        var inicioSegundoCiclo = inicioOriginal.AddHours(3);
        protocolo.IniciarConferencia(inicioSegundoCiclo);
        protocolo.Aprovar(inicioSegundoCiclo.AddMinutes(5)); // 2º ciclo: 5 min

        Assert.Equal(TimeSpan.FromMinutes(25), protocolo.Duracao);
    }

    [Fact]
    public void ReabrirConferenciaDuasVezes_AcumulaOsTresCiclos()
    {
        var protocolo = NovoProtocolo();
        var t0 = DateTimeOffset.UtcNow;
        protocolo.AtribuirA(Guid.NewGuid(), t0);
        protocolo.IniciarConferencia(t0);
        protocolo.Reprovar(t0.AddMinutes(10)); // ciclo 1: 10 min

        protocolo.ReabrirConferencia(t0.AddHours(1));
        protocolo.IniciarConferencia(t0.AddHours(1));
        protocolo.Reprovar(t0.AddHours(1).AddMinutes(15)); // ciclo 2: 15 min

        protocolo.ReabrirConferencia(t0.AddHours(2));
        protocolo.IniciarConferencia(t0.AddHours(2));
        protocolo.Aprovar(t0.AddHours(2).AddMinutes(5)); // ciclo 3: 5 min

        Assert.Equal(TimeSpan.FromMinutes(30), protocolo.Duracao);
    }

    // O ponto inteiro de CicloConferencia (em vez de um TimeSpan acumulado cego) é o Dashboard
    // conseguir saber DE QUEM foi cada ciclo — não só quanto tempo passou. Prova isso direto,
    // não só a soma final.
    [Fact]
    public void ReabrirConferencia_RegistraOCicloComQuemEraODonoNaquelaHora()
    {
        var protocolo = NovoProtocolo();
        var donoId = Guid.NewGuid();
        var inicioOriginal = DateTimeOffset.UtcNow;
        protocolo.AtribuirA(donoId, inicioOriginal);
        protocolo.IniciarConferencia(inicioOriginal);
        protocolo.Reprovar(inicioOriginal.AddMinutes(20));

        protocolo.ReabrirConferencia(inicioOriginal.AddHours(2));

        var ciclo = Assert.Single(protocolo.CiclosAnteriores);
        Assert.Equal(donoId, ciclo.ConferenteId);
        Assert.Equal(inicioOriginal, ciclo.IniciadoEm);
        Assert.Equal(inicioOriginal.AddMinutes(20), ciclo.ConcluidoEm);
        Assert.Equal(TimeSpan.FromMinutes(20), ciclo.Duracao);
    }

    [Fact]
    public void ReabrirConferencia_SemDonoNuncaTerAcontecido_NaoRegistraCiclo()
    {
        // Reabrir um protocolo que nunca chegou a ser conferido de verdade (sem IniciadoEm/
        // ConcluidoEm ainda) não tem ciclo nenhum pra fechar — guarda de nulidade, não é um
        // cenário real (a Application só chama isso pra Aprovado/Reprovado), mas o Domain não
        // deve quebrar nem inventar um ciclo vazio se for chamado fora desse invariante.
        var protocolo = NovoProtocolo();

        protocolo.ReabrirConferencia(DateTimeOffset.UtcNow);

        Assert.Empty(protocolo.CiclosAnteriores);
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
