namespace Dispatch.Application;

// RF-33: ativar, desativar e remover — três ações pequenas o bastante pra não justificar
// três arquivos separados.
public sealed class AtivarRegraAlcada(IRegraAlcadaRepository regras, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid regraId, CancellationToken cancellationToken = default)
    {
        var encontrada = await regras.AtivarAsync(regraId, cancellationToken);
        if (encontrada)
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }

        return encontrada;
    }
}

public sealed class DesativarRegraAlcada(IRegraAlcadaRepository regras, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid regraId, CancellationToken cancellationToken = default)
    {
        var encontrada = await regras.DesativarAsync(regraId, cancellationToken);
        if (encontrada)
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }

        return encontrada;
    }
}

public sealed class RemoverRegraAlcada(IRegraAlcadaRepository regras, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid regraId, CancellationToken cancellationToken = default)
    {
        var encontrada = await regras.RemoverAsync(regraId, cancellationToken);
        if (encontrada)
        {
            await unitOfWork.SalvarAsync(cancellationToken);
        }

        return encontrada;
    }
}
