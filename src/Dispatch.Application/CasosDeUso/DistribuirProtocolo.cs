using Dispatch.Domain;

namespace Dispatch.Application;

// Orquestra o que já existe em Dispatch.Domain via AplicadorDeDistribuicao e persiste o
// efeito. Fluxo avulso (um protocolo só) — o fluxo em lote é ImportarLote.
public sealed class DistribuirProtocolo(
    IConferenteRepository conferentes,
    IEquipeRepository equipes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IProtocoloRepository protocolos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoDistribuicao> ExecutarAsync(
        Protocolo protocolo,
        Escrevente escrevente,
        CancellationToken cancellationToken = default)
    {
        var resultado = AplicadorDeDistribuicao.Executar(
            protocolo,
            escrevente,
            await equipes.ObterTodasAsync(cancellationToken),
            await conferentes.ObterNaEscalaAsync(cancellationToken),
            await regras.ObterAtivasAsync(cancellationToken),
            await tiposAto.ObterTodosAsync(cancellationToken),
            relogio.Agora,
            out _);

        protocolos.Adicionar(protocolo);
        await unitOfWork.SalvarAsync(cancellationToken);

        return resultado;
    }
}
