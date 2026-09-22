using Dispatch.Domain;

namespace Dispatch.Application;

// Pedido do dono: "como distribuidora e admin do sistema, quero editar o tempo de conferência
// de um protocolo" — corrige um valor que saiu errado (ex.: esquecimento de pausar/retomar),
// sem mexer nos horários reais registrados (IniciadoEm/ConcluidoEm/CiclosAnteriores). Só faz
// sentido pra um protocolo já concluído — é o valor exibido no card/histórico/Dashboard que
// está sendo corrigido, não um cronômetro em andamento.
public sealed class AjustarDuracaoProtocolo(
    IProtocoloRepository protocolos,
    IRelogio relogio,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoAjustarDuracaoProtocolo> ExecutarAsync(
        Guid protocoloId, TimeSpan duracaoNova, Guid ajustadoPorId, string? motivo, CancellationToken cancellationToken = default)
    {
        if (duracaoNova < TimeSpan.Zero)
        {
            return ResultadoAjustarDuracaoProtocolo.DuracaoInvalida;
        }

        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return ResultadoAjustarDuracaoProtocolo.NaoEncontrado;
        }

        if (protocolo.Status is not (StatusProtocolo.Aprovado or StatusProtocolo.Reprovado))
        {
            return ResultadoAjustarDuracaoProtocolo.StatusInvalido;
        }

        protocolo.AjustarDuracao(duracaoNova, ajustadoPorId, relogio.Agora, motivo);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoAjustarDuracaoProtocolo.Sucesso;
    }
}

public enum ResultadoAjustarDuracaoProtocolo
{
    Sucesso,
    NaoEncontrado,
    StatusInvalido,
    DuracaoInvalida
}
