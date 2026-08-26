namespace Dispatch.Application;

// RF-27. Pendente: quando existir persistência de Protocolo, marcar ausente aqui também
// precisa devolver os protocolos atribuídos a esta pessoa pro pool — hoje não dá, Protocolo
// ainda não é persistido (ver CLAUDE.md).
public sealed class MarcarPresenca(IConferenteRepository conferentes, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid conferenteId, bool presente, CancellationToken cancellationToken = default)
    {
        var conferente = await conferentes.ObterPorIdAsync(conferenteId, cancellationToken);
        if (conferente is null)
        {
            return false;
        }

        conferente.MarcarPresenca(presente);
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
