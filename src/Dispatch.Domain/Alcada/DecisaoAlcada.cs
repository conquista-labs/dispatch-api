namespace Dispatch.Domain;

// RegraAplicada nulo = decisão veio do padrão aberto (ausência de regra), não de uma regra
// específica. RNF-02 pede que toda decisão automática registre a regra que a originou —
// isto é o registro, propagado até o resultado final do motor de distribuição.
public sealed record DecisaoAlcada(ResultadoAlcada Resultado, RegraAlcada? RegraAplicada);
