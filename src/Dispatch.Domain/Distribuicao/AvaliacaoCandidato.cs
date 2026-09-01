namespace Dispatch.Domain;

public sealed record AvaliacaoCandidato(Conferente Conferente, DecisaoAlcada DecisaoEtapa, DecisaoAlcada DecisaoTipo, DecisaoAlcada DecisaoEquipe)
{
    public bool Elegivel =>
        DecisaoEtapa.Resultado == ResultadoAlcada.Permitido &&
        DecisaoTipo.Resultado == ResultadoAlcada.Permitido &&
        DecisaoEquipe.Resultado == ResultadoAlcada.Permitido;
}
