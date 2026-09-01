namespace Dispatch.Domain;

// Um passo da explicação de uma decisão de alçada (ResolvedorAlcada.Explicar) — uma entrada
// por camada que teve opinião sobre o caso (mais a reserva, se houver), na ordem em que a
// cascata as avaliou. Só existe pra leitura explicativa (painel de detalhe, simulador
// "Testar") — o caminho quente da distribuição usa só Resolver, que não monta essa lista.
public sealed record PassoTrilha(string Camada, ResultadoAlcada Efeito, RegraAlcada? Regra);
