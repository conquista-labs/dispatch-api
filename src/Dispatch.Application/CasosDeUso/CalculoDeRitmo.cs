using Dispatch.Domain;

namespace Dispatch.Application;

// Monta os atos do ritmo (RF-46a/b) a partir dos protocolos concluídos e das referências efetivas já
// calculadas em lote (ReferenciasDeTempoEmLote) — a conta em si é Ritmo.Calcular (Domain). Ato sem tipo
// ou com tipo fora do catálogo não tem referência e fica fora.
internal sealed class CalculoDeRitmo(IReadOnlyDictionary<Guid, TempoDeReferencia> referencias)
{
    // Por pessoa: só os atos que ela concluiu (o chamador passa os do dono atual), com o tempo dos
    // ciclos DELA em cada um (decisão do dono, 25/09/2026).
    public RitmoCalculado? DoConferente(IEnumerable<Protocolo> concluidosPeloConferente, Guid conferenteId) =>
        Ritmo.Calcular(concluidosPeloConferente.Select(p => new AtoParaRitmo(p.TempoDe(conferenteId), ReferenciaDe(p))));

    // Operação inteira (KPI da gestão): Σ Duracao de todos ÷ Σ referência — "quanto tempo os atos
    // levaram", do mesmo jeito que o tempo médio agregado soma o protocolo inteiro.
    public RitmoCalculado? DaOperacao(IEnumerable<Protocolo> concluidos) =>
        Ritmo.Calcular(concluidos.Select(p => new AtoParaRitmo(p.Duracao, ReferenciaDe(p))));

    // RF-46b "Seu tempo por tipo de ato": os mesmos atos elegíveis do ritmo da pessoa, agrupados por
    // tipo, mais volume primeiro (empate: nome, depois id — ordem estável).
    public IReadOnlyList<MeuTempoPorTipo> PorTipo(
        IEnumerable<Protocolo> concluidosPeloConferente, Guid conferenteId, IReadOnlyDictionary<Guid, TipoAto> catalogo) =>
        concluidosPeloConferente
            .Select(p => (TipoAtoId: p.TipoAtoId, Tempo: p.TempoDe(conferenteId)))
            .Where(a => a.Tempo is not null && a.TipoAtoId is { } tipoId && referencias.ContainsKey(tipoId) && catalogo.ContainsKey(tipoId))
            .GroupBy(a => a.TipoAtoId!.Value)
            .Select(g => new MeuTempoPorTipo(
                g.Key,
                catalogo[g.Key].Nome,
                g.Count(),
                TimeSpan.FromTicks((long)g.Average(a => a.Tempo!.Value.Ticks)),
                referencias[g.Key].Minutos))
            .OrderByDescending(t => t.Atos)
            .ThenBy(t => t.Nome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.TipoAtoId)
            .ToList();

    private int? ReferenciaDe(Protocolo protocolo) =>
        protocolo.TipoAtoId is { } tipoId && referencias.TryGetValue(tipoId, out var referencia) ? referencia.Minutos : null;
}
