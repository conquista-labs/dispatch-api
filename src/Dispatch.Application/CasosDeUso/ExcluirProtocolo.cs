namespace Dispatch.Application;

// RF-18i: privilégio da distribuidora. Soft-delete (Protocolo.Excluir) — o front já confirma
// com o dono antes de chamar isso; aqui não há regra de negócio pra checar, só a transição.
public sealed class ExcluirProtocolo(IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(id, cancellationToken);
        if (protocolo is null)
        {
            return false;
        }

        protocolo.Excluir();
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
