using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18a: outra ação do painel de detalhe — atribui a quem tem alçada e está com a carga mais
// baixa agora, sem exigir que o protocolo esteja em exceção (diferente de AtribuirManualmente,
// RF-17, que só resolve exceção e deixa a distribuidora escolher a pessoa). Reaproveita
// VerificadorDeAlcada, mesmo crivo que PegarProtocolo e ObterMinhaFila já usam.
public sealed class AtribuirAoMenosCarregado(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoAtribuirAoMenosCarregado> ExecutarAsync(Guid protocoloId, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoAtribuirAoMenosCarregado.ProtocoloNaoEncontrado;
        }

        if (protocolo.Status is not (StatusProtocolo.Pool or StatusProtocolo.Excecao))
        {
            return ResultadoAtribuirAoMenosCarregado.ProtocoloNaoElegivel;
        }

        var equipeDoEscreventeId = (await escreventes.ObterPorIdAsync(protocolo.EscreventeId, cancellationToken))?.EquipeId;
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var elegiveis = (await conferentes.ObterNaEscalaAsync(cancellationToken))
            .Where(c => VerificadorDeAlcada.TemAlcada(c, protocolo, equipeDoEscreventeId, regrasAtivas))
            .ToList();

        if (elegiveis.Count == 0)
        {
            return ResultadoAtribuirAoMenosCarregado.NinguemComAlcada;
        }

        var escolhido = elegiveis.OrderBy(c => c.CargaAtual).First();
        // Decisão humana explícita (a distribuidora clicou o botão), não decisão automática de
        // uma regra específica — RegraAplicadaId fica nulo, igual AtribuirManualmente/PegarProtocolo.
        protocolo.AtribuirA(escolhido.Id, relogio.Agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoAtribuirAoMenosCarregado.Sucesso;
    }
}

public enum ResultadoAtribuirAoMenosCarregado
{
    Sucesso,
    ProtocoloNaoEncontrado,
    ProtocoloNaoElegivel,
    NinguemComAlcada
}
