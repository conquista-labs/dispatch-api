namespace Dispatch.Application;

// RF-27: marcar ausente devolve pro pool os protocolos já atribuídos a essa pessoa.
public sealed class MarcarPresenca(IConferenteRepository conferentes, IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid conferenteId, bool presente, CancellationToken cancellationToken = default)
    {
        var conferente = await conferentes.ObterPorIdAsync(conferenteId, cancellationToken);
        if (conferente is null)
        {
            return false;
        }

        conferente.MarcarPresenca(presente);

        if (!presente)
        {
            foreach (var protocolo in await protocolos.ObterAtribuidosAAsync(conferenteId, cancellationToken))
            {
                protocolo.EnviarParaPool();
            }
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
