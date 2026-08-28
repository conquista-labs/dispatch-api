namespace Dispatch.Application;

// RF-34f: alimenta o score do conferente (RF-46, Dashboard — ainda não construído). Peso
// mínimo 1 (TipoAto.cs já documenta isso) — clampado aqui porque o domínio não guarda esse
// invariante sozinho (TipoAto.DefinirPesoDeComplexidade aceita qualquer int).
public sealed class DefinirPesoDeComplexidadeDoTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<bool> ExecutarAsync(Guid tipoAtoId, int peso, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return false;
        }

        tipoAto.DefinirPesoDeComplexidade(Math.Max(1, peso));
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
