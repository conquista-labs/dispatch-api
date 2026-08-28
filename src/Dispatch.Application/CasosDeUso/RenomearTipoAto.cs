using Dispatch.Domain;

namespace Dispatch.Application;

// RF-34b: renomear não migra protocolo/regra nenhum — os dois referenciam por Id, não por
// nome (ver Dispatch.Domain/TipoAto.cs), então a migração já é automática.
public sealed class RenomearTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoRenomearTipoAto> ExecutarAsync(Guid tipoAtoId, string nome, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return new ResultadoRenomearTipoAto.NaoEncontrado();
        }

        var nomeNormalizado = NormalizadorDeTexto.ParaNomeProprio(nome);
        var existentes = await tiposAto.ObterTodosAsync(cancellationToken);
        if (existentes.Any(t => t.Id != tipoAtoId && string.Equals(t.Nome, nomeNormalizado, StringComparison.OrdinalIgnoreCase)))
        {
            return new ResultadoRenomearTipoAto.JaExiste();
        }

        tipoAto.Renomear(nomeNormalizado);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoRenomearTipoAto.Sucesso();
    }
}

public abstract record ResultadoRenomearTipoAto
{
    private ResultadoRenomearTipoAto() { }

    public sealed record Sucesso : ResultadoRenomearTipoAto;

    public sealed record NaoEncontrado : ResultadoRenomearTipoAto;

    public sealed record JaExiste : ResultadoRenomearTipoAto;
}
