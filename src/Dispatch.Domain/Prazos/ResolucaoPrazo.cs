namespace Dispatch.Domain;

// Equipe nulo + SemEquipeSinalizado true = caiu no padrão D+1 (RF-09: escrevente sem
// equipe precisa aparecer sinalizado no resumo do lote, não silenciosamente).
public sealed record ResolucaoPrazo(Prazo Prazo, Equipe? Equipe, bool SemEquipeSinalizado);
