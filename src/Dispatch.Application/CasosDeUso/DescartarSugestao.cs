using Dispatch.Domain;

namespace Dispatch.Application;

// RF-40: "descartar silencia a proposta" — com memória (seção 7: "não reaparece por N dias"),
// daí o Descartar do Domain gravar até quando. N vem da tabela `config` (seção 8).
public sealed class DescartarSugestao(
    ISugestaoRepository sugestoes, IConfiguracaoRepository configuracao, IUnitOfWork unitOfWork, IRelogio relogio)
{
    public async Task<bool> ExecutarAsync(Guid sugestaoId, CancellationToken cancellationToken = default)
    {
        var sugestao = await sugestoes.ObterPorIdAsync(sugestaoId, cancellationToken);
        if (sugestao is null || sugestao.Status != StatusSugestao.Pendente)
        {
            return false;
        }

        var agora = relogio.Agora;
        var config = await configuracao.ObterAsync(cancellationToken);
        await sugestoes.DescartarAsync(sugestaoId, agora, agora.AddDays(config.DiasDeMemoriaDescarte), cancellationToken);
        await unitOfWork.SalvarAsync(cancellationToken);
        return true;
    }
}
