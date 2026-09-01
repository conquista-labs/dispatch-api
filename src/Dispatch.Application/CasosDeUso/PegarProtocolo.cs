using Dispatch.Domain;

namespace Dispatch.Application;

// RF-20: conferente pegando um protocolo do pool pra si, na mão — reaproveita o mesmo
// crivo de alçada que já filtra "Minha fila" (VerificadorDeAlcada), só que aqui ele bloqueia
// a ação em vez de só esconder o item da lista.
public sealed class PegarProtocolo(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoPegarProtocolo> ExecutarAsync(
        Guid protocoloId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoPegarProtocolo.NaoEncontrado;
        }

        if (protocolo.Status != StatusProtocolo.Pool)
        {
            return ResultadoPegarProtocolo.NaoEstaNoPool;
        }

        var equipeDoEscreventeId = (await escreventes.ObterPorIdAsync(protocolo.EscreventeId, cancellationToken))?.EquipeId;
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        if (!VerificadorDeAlcada.TemAlcada(conferente, protocolo, equipeDoEscreventeId, regrasAtivas))
        {
            return ResultadoPegarProtocolo.SemAlcada;
        }

        protocolo.AtribuirA(conferente.Id, relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoPegarProtocolo.Sucesso;
    }
}

public enum ResultadoPegarProtocolo
{
    Sucesso,
    NaoEncontrado,
    NaoEstaNoPool,
    SemAlcada
}
