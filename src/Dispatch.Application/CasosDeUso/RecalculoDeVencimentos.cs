using Dispatch.Domain;

namespace Dispatch.Application;

// RF-38: recalcula o vencimento dos protocolos abertos de uma equipe depois que o prazo dela
// muda. Reaproveitado por EditarEquipe (edição direta) e AplicarSugestao (mudança vinda de
// uma proposta de "prazo irreal") — a cascata é a mesma nos dois casos, só muda quem mexeu
// no prazo primeiro.
internal static class RecalculoDeVencimentos
{
    public static async Task AplicarAsync(
        Equipe equipe, IEscreventeRepository escreventes, IProtocoloRepository protocolos, CancellationToken cancellationToken)
    {
        var idsDosEscreventes = (await escreventes.ObterTodosAsync(cancellationToken))
            .Where(e => e.EquipeId == equipe.Id)
            .Select(e => e.Id)
            .ToList();

        if (idsDosEscreventes.Count == 0)
        {
            return;
        }

        var abertos = await protocolos.ObterAbertosPorEscreventesAsync(idsDosEscreventes, cancellationToken);
        foreach (var protocolo in abertos)
        {
            var prazoNovo = equipe.PrazoPara(protocolo.Etapa);
            protocolo.DefinirPrazo(prazoNovo, protocolo.AndamentoEm);
        }
    }
}
