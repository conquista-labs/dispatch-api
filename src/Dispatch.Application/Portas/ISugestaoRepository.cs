using Dispatch.Domain;

namespace Dispatch.Application;

public interface ISugestaoRepository
{
    Task<IReadOnlyCollection<Sugestao>> ObterPendentesAsync(CancellationToken cancellationToken);

    // RF-41: aplicadas e descartadas, com o efeito de cada decisão.
    Task<IReadOnlyCollection<Sugestao>> ObterHistoricoAsync(CancellationToken cancellationToken);

    Task<Sugestao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    // GerarSugestoes usa isso pro dedup (seção 7: "chave única por proposta") — chave não é
    // única na tabela (o histórico guarda uma linha por ciclo de vida), então isso devolve
    // sempre a mais recente pra decidir se atualiza, ignora (memória de descarte) ou cria outra.
    Task<Sugestao?> ObterPorChaveAtivaAsync(string chave, CancellationToken cancellationToken);

    void Adicionar(Sugestao sugestao);

    // Mutação em cima de uma sugestão que já existe passa direto pelo registro rastreado pelo
    // EF, não pelo objeto de Domain devolvido pelas leituras acima — mesmo motivo de
    // RegraAlcadaRepository.AtivarAsync/DesativarAsync não chamarem RegraAlcada.Ativar():
    // o payload (sum type) é "achatado" pra persistência, então o objeto de Domain que
    // ObterPorIdAsync devolve é uma tradução nova a cada chamada, não a instância que o EF
    // rastreia — mutar ele não seria salvo.
    Task AtualizarEvidenciaAsync(
        Guid id, int ocorrencias, string evidencia, double indiceConfianca, DateTimeOffset agora, CancellationToken cancellationToken);

    Task<bool> AplicarAsync(Guid id, DateTimeOffset agora, CancellationToken cancellationToken);

    Task<bool> DescartarAsync(Guid id, DateTimeOffset agora, DateTimeOffset descartarAte, CancellationToken cancellationToken);
}
