using Dispatch.Domain;

namespace Dispatch.Application;

public interface IProtocoloRepository
{
    void Adicionar(Protocolo protocolo);
    Task<Protocolo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Protocolo>> ObterAtribuidosAAsync(Guid conferenteId, CancellationToken cancellationToken);

    // RF-13: loteImportacaoId nulo = todos os protocolos (sem filtrar por lote).
    Task<IReadOnlyCollection<Protocolo>> ObterParaDistribuicaoAsync(Guid? loteImportacaoId, CancellationToken cancellationToken);

    // RF-16: "sem dono" é Pool ou Exceção — não filtra só por DonoId nulo porque Descartado
    // também tem DonoId nulo, e esse não deve voltar a ser redistribuído.
    Task<IReadOnlyCollection<Protocolo>> ObterSemDonoAsync(CancellationToken cancellationToken);

    // RF-38: "aberto" é qualquer coisa que ainda não chegou num status terminal (Aprovado,
    // Reprovado, Descartado) — inclui Exceção de propósito, porque o vencimento dela também
    // fica desatualizado quando o prazo da equipe muda.
    Task<IReadOnlyCollection<Protocolo>> ObterAbertosPorEscreventesAsync(
        IReadOnlyCollection<Guid> escreventeIds, CancellationToken cancellationToken);
}
