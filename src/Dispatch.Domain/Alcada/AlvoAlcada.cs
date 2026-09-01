namespace Dispatch.Domain;

// Mesmo padrão de SujeitoAlcada: um alvo de regra é uma etapa, um tipo de ato, a equipe do
// escrevente ou "todos os atos" (alçada plena, RF-29b) — nunca outra coisa.
public abstract record AlvoAlcada
{
    private AlvoAlcada() { }

    public sealed record PorEtapa(Etapa Etapa) : AlvoAlcada;

    public sealed record PorTipoAto(Guid TipoAtoId) : AlvoAlcada;

    // Guid? nulo é "sem equipe" como alvo válido (RF-29a) — não "esta regra não é sobre
    // equipe". O simulador do protótipo trata "sem equipe" como mais um valor de equipe.
    public sealed record PorEquipeDeEscrevente(Guid? EquipeId) : AlvoAlcada;

    // Alçada plena (RF-29b) — sem payload. Só é consultado dentro da família Tipo (ver
    // ResolvedorAlcada) e cede a uma regra de Nega específica do mesmo escopo.
    public sealed record PorTodosOsAtos : AlvoAlcada;
}
