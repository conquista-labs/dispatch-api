using Dispatch.Domain;

namespace Dispatch.Application;

public interface IProtocoloRepository
{
    void Adicionar(Protocolo protocolo);
    Task<Protocolo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    // Busca em lote, mesmo molde de IUsuarioRepository.ObterVariosPorIdsAsync — evita N+1 em
    // leituras que resolvem vários protocolos por id de uma vez (ex.: pedidos de reabertura
    // pendentes, achado numa auditoria de qualidade).
    Task<IReadOnlyCollection<Protocolo>> ObterVariosPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Protocolo>> ObterAtribuidosAAsync(Guid conferenteId, CancellationToken cancellationToken);

    // RF-13: loteImportacaoId nulo = todos os protocolos (sem filtrar por lote). Usado por
    // GerarSugestoes (precisa do histórico completo pros cálculos de moda/percentil) e
    // ListarTiposAtoComUso (contagem de uso real) — nenhum corte de data aqui, de propósito.
    Task<IReadOnlyCollection<Protocolo>> ObterParaDistribuicaoAsync(Guid? loteImportacaoId, CancellationToken cancellationToken);

    // RF-13 (visão de Distribuição): mesma pergunta de ObterParaDistribuicaoAsync, mas com uma
    // janela de data aplicada só aos status terminais (Aprovado/Reprovado) quando nenhum lote
    // específico é pedido — Pool/Atribuído/Conferindo/Exceção (trabalho em andamento) sempre
    // voltam por inteiro, independente da idade, porque ficam pequenos por natureza. Descartado
    // nunca aparece em bucket nenhum desta tela e sai da consulta. Método dedicado — não
    // reaproveita ObterParaDistribuicaoAsync porque esse também alimenta GerarSugestoes e
    // ListarTiposAtoComUso (ver comentário acima) — nenhum dos dois pode ter o resultado
    // silenciosamente cortado por uma janela pensada só pra esta tela.
    Task<IReadOnlyCollection<Protocolo>> ObterParaVisaoDistribuicaoAsync(
        Guid? loteImportacaoId, DateTimeOffset concluidosDesde, CancellationToken cancellationToken);

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

    // RF-46c (mediana do tempo de referência): a duração (Protocolo.Duracao — ciclos + ajuste manual) de
    // cada conferência Aprovada/Reprovada com `ConcluidoEm >= desde`, só dos tipos pedidos. Projetada no
    // recorte leve (tipo + duração) numa query só — o histórico de 12 meses de um tipo pode ter milhares
    // de linhas, e materializar Protocolo traria colunas e pausas que a conta não usa. Linha sem duração
    // (nunca iniciada) não vem.
    Task<IReadOnlyCollection<DuracaoDeConferencia>> ObterDuracoesConcluidasPorTipoAsync(
        IReadOnlyCollection<Guid> tipoAtoIds, DateTimeOffset desde, CancellationToken cancellationToken);

    // RF-33: "contador de aplicações" de cada regra de alçada — leitura agregada, igual
    // CargaAtual/Semaforo, nunca persistida na própria regra. Em lote (uma query agrupada, não
    // uma por regra) — achado numa investigação de lentidão real em produção: GET
    // /regras-alcada chamava a versão por-regra dentro de um foreach (N+1: 1 query pra listar
    // + 1 por regra), ~96 round-trips sequenciais contra o Neon pra ~95 regras. Regra sem
    // nenhum protocolo aplicado não aparece na coleção — quem lê trata ausência como 0.
    Task<IReadOnlyCollection<(Guid RegraAlcadaId, int Total)>> ContarPorRegraAplicadaAsync(CancellationToken cancellationToken);

    // Continuidade de conferência + histórico do painel de detalhe: todas as linhas (qualquer
    // status/lote) com esses números. Numero não é único de propósito — um mesmo item pode ter
    // várias linhas ao longo do tempo (reprocessamento, RF-07).
    Task<IReadOnlyCollection<Protocolo>> ObterPorNumerosAsync(IReadOnlyCollection<string> numeros, CancellationToken cancellationToken);

    // RF-24k ("2ª conferência"): o mesmo histórico, mas projetado no recorte leve que a regra
    // precisa — uma listagem inteira (Minha fila, Distribuição) busca os seus números numa query
    // só, sem carregar as coleções filhas (ciclos, pausas, ajustes) de cada linha.
    Task<IReadOnlyCollection<RegistroDoNumero>> ObterRegistrosPorNumerosAsync(IReadOnlyCollection<string> numeros, CancellationToken cancellationToken);

    // RF-30: "tipos em circulação" pro aviso de cobertura de alçada — só os TipoAtoId distintos,
    // resolvido como SELECT DISTINCT no banco em vez de carregar a tabela protocolos inteira
    // pra memória só pra extrair isso (achado na mesma auditoria de performance dos índices).
    Task<IReadOnlyCollection<Guid>> ObterTipoAtoIdsDistintosAsync(CancellationToken cancellationToken);
}
