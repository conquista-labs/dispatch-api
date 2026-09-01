namespace Dispatch.Domain;

// Motor v3: uma decisão só por candidato (o caso inteiro — etapa+tipo+equipe — resolvido numa
// cascata, ver ResolvedorAlcada), não mais 3 decisões independentes por dimensão.
public sealed record AvaliacaoCandidato(Conferente Conferente, DecisaoAlcada Decisao)
{
    public bool Elegivel => Decisao.Resultado == ResultadoAlcada.Permitido;
}
