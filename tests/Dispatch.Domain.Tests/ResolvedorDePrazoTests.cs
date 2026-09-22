namespace Dispatch.Domain.Tests;

public class ResolvedorDePrazoTests
{
    // Quarta-feira 11h em Brasília (14h UTC) — só serve pra ter uma referência arbitrária,
    // nenhum destes testes configura corte de horário.
    private static readonly DateTimeOffset Referencia = new(2026, 8, 26, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EscreventeComEquipe_UsaOPrazoDaEquipeParaAEtapa()
    {
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);

        var resolucao = ResolvedorDePrazo.Resolver(escrevente, Etapa.PosConferencia, Referencia, [equipe]);

        Assert.Equal(TipoPrazo.D1, resolucao.Prazo.Tipo);
        Assert.Equal(equipe, resolucao.Equipe);
        Assert.False(resolucao.SemEquipeSinalizado);
    }

    [Fact]
    public void EscreventeSemEquipe_CaiNoPadraoD1ESinaliza()
    {
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId: null);

        var resolucao = ResolvedorDePrazo.Resolver(escrevente, Etapa.PreConferencia, Referencia, []);

        Assert.Equal(TipoPrazo.D1, resolucao.Prazo.Tipo);
        Assert.Null(resolucao.Equipe);
        Assert.True(resolucao.SemEquipeSinalizado);
    }

    [Fact]
    public void EscreventeComEquipeIdQueNaoExisteNaLista_CaiNoPadraoD1ESinaliza()
    {
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", Guid.NewGuid());

        var resolucao = ResolvedorDePrazo.Resolver(escrevente, Etapa.PreConferencia, Referencia, []);

        Assert.Equal(TipoPrazo.D1, resolucao.Prazo.Tipo);
        Assert.True(resolucao.SemEquipeSinalizado);
    }

    // Corte de horário configurado na equipe — entrada depois do corte (comparado em horário
    // de Brasília) devolve CorteDeHorario, não o TipoPrazo base.
    [Fact]
    public void EscreventeComEquipeComCorteDeHorario_DepoisDoCorte_UsaCorteDeHorario()
    {
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(
            equipeId, "Quinto Andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D1),
            cortePosConferenciaHorarioCorte: new TimeOnly(16, 0),
            cortePosConferenciaHorarioVencimento: new TimeOnly(10, 0));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);
        var depoisDoCorte = new DateTimeOffset(2026, 8, 26, 19, 30, 0, TimeSpan.Zero); // 16h30 em Brasília

        var resolucao = ResolvedorDePrazo.Resolver(escrevente, Etapa.PosConferencia, depoisDoCorte, [equipe]);

        Assert.Equal(TipoPrazo.CorteDeHorario, resolucao.Prazo.Tipo);
        Assert.Equal(new TimeOnly(10, 0), resolucao.Prazo.HorarioDeVencimento);
    }
}
