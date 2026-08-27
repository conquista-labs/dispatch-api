using Dispatch.Domain;

namespace Dispatch.Application;

// RF-40: "descartar silencia a proposta" — com memória (seção 7: "não reaparece por N dias"),
// daí o Descartar do Domain gravar até quando. N hardcoded por ora, mesma pendência das
// faixas do semáforo e do limite de simultâneos — tabela `config` (seção 8) ainda não existe.
public sealed class DescartarSugestao(ISugestaoRepository sugestoes, IUnitOfWork unitOfWork, IRelogio relogio)
{
    private const int DiasDeMemoria = 30;

    public async Task<bool> ExecutarAsync(Guid sugestaoId, CancellationToken cancellationToken = default)
    {
        var sugestao = await sugestoes.ObterPorIdAsync(sugestaoId, cancellationToken);
        if (sugestao is null || sugestao.Status != StatusSugestao.Pendente)
        {
            return false;
        }

        var agora = relogio.Agora;
        await sugestoes.DescartarAsync(sugestaoId, agora, agora.AddDays(DiasDeMemoria), cancellationToken);
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
