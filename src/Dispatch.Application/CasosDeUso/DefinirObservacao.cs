namespace Dispatch.Application;

// RF-15 (Distribuidora, sem restrição) e RF-23 (o próprio conferente dono, restrito ao que é
// dele) chamam o mesmo caso de uso — `conferenteRestritoId` nulo é o caminho da Distribuidora,
// preenchido é o do conferente, e nesse caso `DonoId` precisa bater ou a ação é negada.
public sealed class DefinirObservacao(IProtocoloRepository protocolos, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoDefinirObservacao> ExecutarAsync(
        Guid protocoloId, string? observacao, Guid? conferenteRestritoId = null, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoDefinirObservacao.NaoEncontrado;
        }

        if (conferenteRestritoId is { } conferenteId && protocolo.DonoId != conferenteId)
        {
            return ResultadoDefinirObservacao.NaoEhSeu;
        }

        protocolo.DefinirObservacao(observacao);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoDefinirObservacao.Sucesso;
    }
}

public enum ResultadoDefinirObservacao
{
    Sucesso,
    NaoEncontrado,
    NaoEhSeu
}
