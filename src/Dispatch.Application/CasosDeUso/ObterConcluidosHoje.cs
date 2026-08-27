using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24: indicadores do dia do próprio conferente — "hoje" é local ao caso de uso porque
// só ele decide o que "início do dia" significa (via IRelogio), a porta só sabe filtrar
// por "desde" um instante.
public sealed class ObterConcluidosHoje(
    IProtocoloRepository protocolos,
    IRelogio relogio)
{
    public async Task<IReadOnlyCollection<Protocolo>> ExecutarAsync(
        Conferente conferente, CancellationToken cancellationToken = default)
    {
        var inicioDoDia = new DateTimeOffset(relogio.Agora.Date, relogio.Agora.Offset);
        return await protocolos.ObterConcluidosPorConferenteAsync(conferente.Id, inicioDoDia, cancellationToken);
    }
}
