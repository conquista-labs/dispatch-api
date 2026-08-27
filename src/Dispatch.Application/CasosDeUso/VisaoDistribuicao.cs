using Dispatch.Domain;

namespace Dispatch.Application;

// RF-13: "por status" (pool/atribuídos/em conferência/concluídos são buckets distintos aqui)
// e "por conferente" (atribuídos + em conferência quebrados por dono) — exceção é sua própria
// visão, fora do agrupamento por status. As mesmas listas se sobrepõem entre as visões de
// propósito: cada uma é um jeito diferente de olhar pro mesmo conjunto de protocolos.
public sealed record VisaoDistribuicao(
    IReadOnlyList<Protocolo> Pool,
    IReadOnlyList<Protocolo> Atribuidos,
    IReadOnlyList<Protocolo> EmConferencia,
    IReadOnlyList<Protocolo> Concluidos,
    IReadOnlyList<Protocolo> Excecoes,
    IReadOnlyList<GrupoPorConferente> PorConferente);

public sealed record GrupoPorConferente(Guid ConferenteId, IReadOnlyList<Protocolo> Protocolos);
