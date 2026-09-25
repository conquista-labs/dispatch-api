using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24k pra uma listagem inteira: uma query só pelos números distintos (em vez de uma por
// protocolo), e a regra do Domain aplicada em memória. Quem lê trata id ausente como 1ª
// conferência — o dicionário cobre todos os protocolos recebidos, mas o default é o seguro.
internal static class NumeroDaConferenciaEmLote
{
    public static async Task<IReadOnlyDictionary<Guid, int>> CalcularAsync(
        IProtocoloRepository protocolos,
        IReadOnlyCollection<Protocolo> alvo,
        CancellationToken cancellationToken)
    {
        if (alvo.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var numeros = alvo.Select(p => p.Numero).Distinct().ToList();
        var historicoPorNumero = (await protocolos.ObterRegistrosPorNumerosAsync(numeros, cancellationToken))
            .ToLookup(r => r.Numero);

        return alvo
            .DistinctBy(p => p.Id)
            .ToDictionary(
                p => p.Id,
                p => ResolvedorDeContinuidade.NumeroDaConferencia(RegistroDoNumero.De(p), historicoPorNumero[p.Numero]));
    }
}
