using Dispatch.Domain;

namespace Dispatch.Application;

// RF-46c pra uma listagem inteira (página de Tipos de ato, Dashboard): UMA query pelo histórico de
// 12 meses de todos os tipos pedidos, e a regra do Domain aplicada em memória por tipo — nunca uma
// query por tipo. Sem cache de propósito (ADR-0043): a mediana muda a cada conclusão, correção,
// reabertura e ajuste de duração, e a estimativa a cada troca de peso ou de TempoMedioPorAtoMinutos —
// invalidar tudo isso custaria mais que a consulta indexada, e não há job pra recalcular (gaps §29).
internal static class ReferenciasDeTempoEmLote
{
    public static async Task<IReadOnlyDictionary<Guid, TempoDeReferencia>> CalcularAsync(
        IProtocoloRepository protocolos,
        IReadOnlyCollection<TipoAto> tipos,
        double tempoMedioPorAtoMinutos,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        if (tipos.Count == 0)
        {
            return new Dictionary<Guid, TempoDeReferencia>();
        }

        var distintos = tipos.DistinctBy(t => t.Id).ToList();
        var duracoesPorTipo = (await protocolos.ObterDuracoesConcluidasPorTipoAsync(
                distintos.Select(t => t.Id).ToList(), TempoDeReferencia.InicioDaJanela(agora), cancellationToken))
            .ToLookup(d => d.TipoAtoId, d => d.Duracao);

        return distintos.ToDictionary(
            t => t.Id,
            t => TempoDeReferencia.Calcular(t.TempoReferenciaMinutos, t.PesoComplexidade, tempoMedioPorAtoMinutos, duracoesPorTipo[t.Id]));
    }
}
