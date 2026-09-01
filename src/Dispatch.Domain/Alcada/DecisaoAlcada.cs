namespace Dispatch.Domain;

// Qual dimensão do caso motivou um bloqueio — usado só pra dar uma pista ao usuário do "por
// quê" (RNF-02/UX do simulador "Testar"), não pela lógica de decisão em si. Simplificação
// consciente em relação ao protótipo: o simulador distingue "negado especificamente nesta
// dimensão" de "fora da lista fechada desta dimensão" com frases diferentes; aqui as duas
// colapsam num motivo só por dimensão — o nome próprio (tipo/equipe) que completaria a frase
// mora no front, que já tem o lookup pronto (mesmo padrão de fraseDaRegra), não faz sentido
// Domain carregar Equipe/nomes de outros conferentes só pra formatar texto.
public enum MotivoAlcada
{
    Etapa,
    Tipo,
    Grupo,
    Equipe,
    Geral,
    Reservado
}

// RegraAplicada nulo = decisão veio do padrão aberto (ausência de regra), não de uma regra
// específica. RNF-02 pede que toda decisão automática registre a regra que a originou —
// isto é o registro, propagado até o resultado final do motor de distribuição.
public sealed record DecisaoAlcada(ResultadoAlcada Resultado, RegraAlcada? RegraAplicada, MotivoAlcada? Motivo = null);
