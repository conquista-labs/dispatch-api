namespace Dispatch.Application;

// RF-25 "remover" — soft delete (Usuario.Desativar) + sai da escala, não apaga a linha.
// RF-27: remover também devolve pro pool os protocolos atribuídos a essa pessoa.
public sealed class RemoverConferente(
    IConferenteRepository conferentes,
    IUsuarioRepository usuarios,
    IProtocoloRepository protocolos,
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

        foreach (var protocolo in await protocolos.ObterAtribuidosAAsync(conferenteId, cancellationToken))
        {
            protocolo.EnviarParaPool();
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
