using Dispatch.Domain;

namespace Dispatch.Application;

// RF-35 (renomear) + RF-36 (prazo por etapa). RF-38 (recalcular vencimentos abertos) fica de
// fora — depende de Protocolo saber de qual Escrevente ele veio, que ainda não existe.
public sealed class EditarEquipe(IEquipeRepository equipes, IUnitOfWork unitOfWork)
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
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
