namespace Dispatch.Domain;

public static class ResolvedorDePrazo
{
    private static readonly Prazo PadraoSemEquipe = new(TipoPrazo.D1);

    public static ResolucaoPrazo Resolver(Escrevente escrevente, Etapa etapa, IReadOnlyCollection<Equipe> equipes)
    {
        var equipe = escrevente.EquipeId is { } equipeId
            ? equipes.FirstOrDefault(e => e.Id == equipeId)
            : null;

        return equipe is null
            ? new ResolucaoPrazo(PadraoSemEquipe, Equipe: null, SemEquipeSinalizado: true)
            : new ResolucaoPrazo(equipe.PrazoPara(etapa), equipe, SemEquipeSinalizado: false);
    }
}
