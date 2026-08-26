using Dispatch.Domain;

namespace Dispatch.Application;

// RF-25 (editar) + RF-26.
public sealed class EditarNivelEJornada(IConferenteRepository conferentes, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid conferenteId, Nivel nivel, double jornadaHoras, CancellationToken cancellationToken = default)
    {
        var conferente = await conferentes.ObterPorIdAsync(conferenteId, cancellationToken);
        if (conferente is null)
        {
            return false;
        }

        conferente.AtualizarNivelEJornada(nivel, jornadaHoras);
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
