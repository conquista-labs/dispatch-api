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

    // RF-19 (coluna "pool disponível").
    Task<IReadOnlyCollection<Protocolo>> ObterPoolAsync(CancellationToken cancellationToken);

    // RF-19 (coluna "em conferência") e RF-21 (contar quantos já estão em conferência, pro
    // limite de simultâneos).
    Task<IReadOnlyCollection<Protocolo>> ObterEmConferenciaPorConferenteAsync(Guid conferenteId, CancellationToken cancellationToken);

    // RF-24: concluídos (aprovado ou reprovado) do dia, só de quem pediu.
    Task<IReadOnlyCollection<Protocolo>> ObterConcluidosPorConferenteAsync(
        Guid conferenteId, DateTimeOffset desde, CancellationToken cancellationToken);

    // RF-34e: "em uso" pra bloquear exclusão de tipo de ato — existence check em vez de
    // carregar a coleção inteira e contar em memória (só a pergunta importa, não a lista).
    Task<bool> ExisteComTipoAtoAsync(Guid tipoAtoId, CancellationToken cancellationToken);

    // RF-46 (Dashboard): concluídos de TODOS os donos no período, não só de um conferente —
    // diferente de ObterConcluidosPorConferenteAsync, que já existia pra RF-24.
    Task<IReadOnlyCollection<Protocolo>> ObterConcluidosNoPeriodoAsync(
        DateTimeOffset desde, DateTimeOffset ate, CancellationToken cancellationToken);

    // RF-33: "contador de aplicações" de uma regra de alçada — leitura agregada, igual
    // CargaAtual/Semaforo, nunca persistida na própria regra.
    Task<int> ContarComRegraAplicadaAsync(Guid regraAlcadaId, CancellationToken cancellationToken);
}
