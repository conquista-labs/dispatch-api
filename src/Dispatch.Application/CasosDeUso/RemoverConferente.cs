namespace Dispatch.Application;

// RF-25 "remover" — soft delete (Usuario.Desativar) + sai da escala, não apaga a linha.
// Mesma pendência de MarcarPresenca quanto a devolver protocolos pro pool.
public sealed class RemoverConferente(
    IConferenteRepository conferentes,
    IUsuarioRepository usuarios,
    IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid conferenteId, CancellationToken cancellationToken = default)
    {
        var conferente = await conferentes.ObterPorIdAsync(conferenteId, cancellationToken);
        if (conferente is null)
        {
            return false;
        }

        var usuario = await usuarios.ObterPorIdAsync(conferente.UsuarioId, cancellationToken);
        usuario?.Desativar();
        conferente.MarcarPresenca(false);

        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
