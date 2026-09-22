using Dispatch.Domain;

namespace Dispatch.Application;

// RF-35 (criar).
public sealed class CriarEquipe(IEquipeRepository equipes, IUnitOfWork unitOfWork)
{
    public async Task<Guid> ExecutarAsync(
        string nome, Prazo prazoPreConferencia, Prazo prazoPosConferencia,
        TimeOnly? cortePreConferenciaHorarioCorte = null, TimeOnly? cortePreConferenciaHorarioVencimento = null,
        TimeOnly? cortePosConferenciaHorarioCorte = null, TimeOnly? cortePosConferenciaHorarioVencimento = null,
        CancellationToken cancellationToken = default)
    {
        var equipe = new Equipe(
            Guid.NewGuid(), nome, prazoPreConferencia, prazoPosConferencia,
            cortePreConferenciaHorarioCorte, cortePreConferenciaHorarioVencimento,
            cortePosConferenciaHorarioCorte, cortePosConferenciaHorarioVencimento);
        equipes.Adicionar(equipe);
        await unitOfWork.SalvarAsync(cancellationToken);
        return equipe.Id;
    }
}
