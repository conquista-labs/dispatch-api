namespace Dispatch.Domain;

// Saída pura do gerador (seção 7) — ainda não é uma Sugestao persistida. Quem decide se vira
// uma linha nova, atualiza uma existente ou é ignorada por descarte-com-memória é o caso de
// uso, que tem acesso ao que já está gravado; o Domain só sabe calcular "o que os dados dizem
// agora", sem saber de histórico.
//
// IndiceConfianca (0.0–1.0): nem o requisito nem o protótipo aprovado definem uma fórmula real
// (protótipo mostra um número mockado) — decisão de projeto: reaproveitar a proporção que cada
// função do GeradorDeSugestoes já calcula internamente pra comparar com o próprio limiar
// (força da moda do nível, percentual de estouro, dominância da equipe, percentual de
// reprovação), em vez de inventar um peso novo. Ver CLAUDE.md.
public sealed record CandidatoSugestao(string Chave, PayloadSugestao Payload, string Evidencia, int Ocorrencias, double IndiceConfianca);
