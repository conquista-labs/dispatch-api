namespace Dispatch.Domain;

public sealed record AvaliacaoCandidato(Conferente Conferente, DecisaoAlcada DecisaoEtapa, DecisaoAlcada DecisaoTipo)
{
    public bool Elegivel =>
        DecisaoEtapa.Resultado == ResultadoAlcada.Permitido &&
        DecisaoTipo.Resultado == ResultadoAlcada.Permitido;
}
