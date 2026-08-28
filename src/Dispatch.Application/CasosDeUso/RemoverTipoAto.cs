using Dispatch.Domain;

namespace Dispatch.Application;

// RF-34e: exclusão de verdade (diferente de RemoverConferente, que é soft delete) — mas só
// quando "sem nenhum uso", checado contra as duas coisas que referenciam TipoAtoId por Guid
// solto, sem FK (ver CLAUDE.md, "Persistência de Protocolo"/"Central de Regras"): protocolos
// já distribuídos com esse tipo, e regras de alçada com AlvoAlcada.PorTipoAto apontando pra
// ele. Mesclar dois tipos (RF-34c) fica de fora — é uma operação maior (migra as duas
// referências pra um Id novo em vez de só bloquear), documentada como próximo passo separado.
public sealed class RemoverTipoAto(ITipoAtoRepository tiposAto, IProtocoloRepository protocolos, IRegraAlcadaRepository regras, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoRemoverTipoAto> ExecutarAsync(Guid tipoAtoId, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return new ResultadoRemoverTipoAto.NaoEncontrado();
        }

        if (await protocolos.ExisteComTipoAtoAsync(tipoAtoId, cancellationToken))
        {
            return new ResultadoRemoverTipoAto.EmUso();
        }

        var todasAsRegras = await regras.ObterTodasAsync(cancellationToken);
        if (todasAsRegras.Any(r => r.Alvo is AlvoAlcada.PorTipoAto porTipo && porTipo.TipoAtoId == tipoAtoId))
        {
            return new ResultadoRemoverTipoAto.EmUso();
        }

        tiposAto.Remover(tipoAto);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoRemoverTipoAto.Sucesso();
    }
}

public abstract record ResultadoRemoverTipoAto
{
    private ResultadoRemoverTipoAto() { }

    public sealed record Sucesso : ResultadoRemoverTipoAto;

    public sealed record NaoEncontrado : ResultadoRemoverTipoAto;

    public sealed record EmUso : ResultadoRemoverTipoAto;
}
