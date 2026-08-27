using Dispatch.Domain;

namespace Dispatch.Application;

// RF-35 (renomear) + RF-36 (prazo por etapa) + RF-38 (recalcular vencimentos abertos).
public sealed class EditarEquipe(
    IEquipeRepository equipes,
    IEscreventeRepository escreventes,
    IProtocoloRepository protocolos,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(
        Guid equipeId, string nome, Prazo prazoPreConferencia, Prazo prazoPosConferencia, CancellationToken cancellationToken = default)
    {
        var equipe = await equipes.ObterPorIdAsync(equipeId, cancellationToken);
        if (equipe is null)
        {
            return false;
        }

        equipe.Renomear(nome);
        equipe.DefinirPrazos(prazoPreConferencia, prazoPosConferencia);

        // RF-38: todo protocolo aberto de quem está nessa equipe recalcula o vencimento com
        // o prazo novo — o momentoDeReferencia continua sendo o AndamentoEm original de cada
        // protocolo, não "agora" (a regra de negócio não mudou, só o prazo que ela usa).
        await RecalculoDeVencimentos.AplicarAsync(equipe, escreventes, protocolos, cancellationToken);

        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
