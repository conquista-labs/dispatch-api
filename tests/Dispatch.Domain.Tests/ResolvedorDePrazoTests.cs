namespace Dispatch.Domain.Tests;

public class ResolvedorDePrazoTests
{
    [Fact]
    public void EscreventeComEquipe_UsaOPrazoDaEquipeParaAEtapa()
    {
        var equipeId = Guid.NewGuid();
        var equipe = new Equipe(equipeId, "5º andar", new Prazo(TipoPrazo.D0), new Prazo(TipoPrazo.D1));
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId);

        var resolucao = ResolvedorDePrazo.Resolver(escrevente, Etapa.PosConferencia, [equipe]);

        Assert.Equal(TipoPrazo.D1, resolucao.Prazo.Tipo);
        Assert.Equal(equipe, resolucao.Equipe);
        Assert.False(resolucao.SemEquipeSinalizado);
    }

    [Fact]
    public void EscreventeSemEquipe_CaiNoPadraoD1ESinaliza()
    {
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", equipeId: null);

        var resolucao = ResolvedorDePrazo.Resolver(escrevente, Etapa.PreConferencia, []);

        Assert.Equal(TipoPrazo.D1, resolucao.Prazo.Tipo);
        Assert.Null(resolucao.Equipe);
        Assert.True(resolucao.SemEquipeSinalizado);
    }

    [Fact]
    public void EscreventeComEquipeIdQueNaoExisteNaLista_CaiNoPadraoD1ESinaliza()
    {
        var escrevente = new Escrevente(Guid.NewGuid(), "Fulano", Guid.NewGuid());

        var resolucao = ResolvedorDePrazo.Resolver(escrevente, Etapa.PreConferencia, []);

        Assert.Equal(TipoPrazo.D1, resolucao.Prazo.Tipo);
        Assert.True(resolucao.SemEquipeSinalizado);
    }
}
