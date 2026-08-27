namespace Dispatch.Domain;

// Saída pura do gerador (seção 7) — ainda não é uma Sugestao persistida. Quem decide se vira
// uma linha nova, atualiza uma existente ou é ignorada por descarte-com-memória é o caso de
// uso, que tem acesso ao que já está gravado; o Domain só sabe calcular "o que os dados dizem
// agora", sem saber de histórico.
public sealed record CandidatoSugestao(string Chave, PayloadSugestao Payload, string Evidencia, int Ocorrencias);
