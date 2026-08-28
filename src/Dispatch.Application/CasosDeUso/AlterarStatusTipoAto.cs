namespace Dispatch.Application;

// RF-34d: ativar e desativar — duas ações pequenas o bastante pra não justificar dois
// arquivos, mesmo padrão de AlterarStatusRegraAlcada.cs. Diferente de RegraAlcada, TipoAto é
// mapeado direto (não tem Registro achatado — ver TipoAtoRepository), então o objeto que
// ObterPorIdAsync devolve já é o rastreado pelo EF Core; mutar e chamar SalvarAsync basta.
public sealed class AtivarTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid tipoAtoId, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return false;
        }

        tipoAto.Ativar();
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}

public sealed class DesativarTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid tipoAtoId, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return false;
        }

        tipoAto.Desativar();
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
