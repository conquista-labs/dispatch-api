namespace Dispatch.Domain;

// Mesmo padrão de SujeitoAlcada: um alvo de regra é uma etapa ou um tipo de ato, nunca outra coisa.
public abstract record AlvoAlcada
{
    private AlvoAlcada() { }

    public sealed record PorEtapa(Etapa Etapa) : AlvoAlcada;

    public sealed record PorTipoAto(Guid TipoAtoId) : AlvoAlcada;
}
