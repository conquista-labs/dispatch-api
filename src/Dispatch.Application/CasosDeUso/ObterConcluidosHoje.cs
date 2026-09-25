using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24: indicadores do dia do próprio conferente — "hoje" é local ao caso de uso porque
// só ele decide o que "início do dia" significa (via IRelogio), a porta só sabe filtrar
// por "desde" um instante. "Hoje" é o dia de Brasília (FusoHorario), não o dia UTC — pelo
// UtcNow.Date o dia virava às 21h locais e zerava os concluídos da noite.
public sealed class ObterConcluidosHoje(
    IProtocoloRepository protocolos,
    IRelogio relogio)
{
    public async Task<IReadOnlyCollection<Protocolo>> ExecutarAsync(
        Conferente conferente, CancellationToken cancellationToken = default)
    {
        var inicioDoDia = FusoHorario.InicioDoDiaLocal(relogio.Agora);
        return await protocolos.ObterConcluidosPorConferenteAsync(conferente.Id, inicioDoDia, cancellationToken);
    }
}
