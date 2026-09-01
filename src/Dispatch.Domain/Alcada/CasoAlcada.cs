namespace Dispatch.Domain;

// O "caso" que o motor de alçada v3 resolve de uma vez só — etapa + tipo + equipe do
// escrevente, as 3 dimensões que uma regra pode mirar (mais o grupo do tipo, derivado de
// TipoAto.Grupo). Substitui o Resolver(alvo único) do v2: a cascata de camadas (ver
// ResolvedorAlcada) decide o caso inteiro numa passada, não uma dimensão isolada por vez —
// uma regra de equipe pode sobrescrever uma negação de etapa do nível, por exemplo, então não
// dá mais pra resolver os 3 eixos de forma independente.
public sealed record CasoAlcada(Etapa Etapa, TipoAto TipoAto, Guid? EquipeId);
