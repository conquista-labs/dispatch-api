namespace Dispatch.Domain;

// Continuidade de conferência — pedido do dono, não é RF numerado nem está no protótipo
// aprovado (o documento de requisitos trata "volta pro mesmo escrevente?" como pergunta em
// aberto, nunca respondida). Quando um protocolo Reprovado reaparece num relatório seguinte
// (RF-07/"linha de corte") na mesma etapa, quem fez a primeira conferência tem prioridade
// sobre o resto do motor — ver MotorDistribuicao.Distribuir.
public static class ResolvedorDeContinuidade
{
    // "Primeira conferência" = a linha mais antiga (AndamentoEm menor) com essa etapa e um
    // dono — não a mais recente, honrando a palavra exata do pedido. Uma linha sem dono (só
    // passou pelo pool/exceção, nunca foi atribuída) não conta como conferência nenhuma.
    public static Guid? Resolver(IReadOnlyCollection<Protocolo> historicoDoNumero, Etapa etapaAtual) =>
        historicoDoNumero
            .Where(p => p.Etapa == etapaAtual && p.DonoId is not null)
            .OrderBy(p => p.AndamentoEm)
            .FirstOrDefault()
            ?.DonoId;

    // Cadastro manual (RF-18f) segue o mesmo fluxo da importação — Número duplicado não é
    // bloqueio automático, é continuidade quando fizer sentido. Mas cadastro manual não tem a
    // "linha de corte" da importação como proteção contra duplicata por engano, então mantém um
    // bloqueio próprio, só relaxado pro cenário real de continuidade: só pode recriar um Número
    // se TODO registro existente já chegou a um estado que não está mais "em uso" (Reprovado —
    // o cenário que motivou isso — Descartado ou Excluído). Qualquer coisa ainda ativa ou já
    // Aprovada continua bloqueando com 409, igual sempre foi.
    public static bool PodeRecriar(IReadOnlyCollection<Protocolo> historicoDoNumero) =>
        historicoDoNumero.All(p => p.Status is StatusProtocolo.Reprovado or StatusProtocolo.Descartado or StatusProtocolo.Excluido);

    // RF-24k — "2ª conferência" (3ª, 4ª...). É 1 + as OUTRAS linhas do mesmo Número, na mesma
    // etapa, com andamento anterior e que terminaram Reprovadas. Só Reprovado conta como "voltou":
    // linha ainda aberta, Aprovada (inclusive corrigida de Reprovado), Descartada ou Excluída não é
    // uma conferência não aprovada. Andamento igual ou posterior não conta, então a linha antiga
    // continua sendo a 1ª mesmo depois que a nova também for reprovada.
    //
    // Calculado na leitura, nunca gravado (ADR-0038): um valor armazenado ficaria desatualizado
    // com edição de etapa, excluir/restaurar uma linha anterior, correção Reprovado→Aprovado e
    // dois do mesmo Número no mesmo lote.
    public static int NumeroDaConferencia(RegistroDoNumero atual, IEnumerable<RegistroDoNumero> historico) =>
        1 + historico.Count(r =>
            r.Id != atual.Id
            && r.Numero == atual.Numero
            && r.Etapa == atual.Etapa
            && r.AndamentoEm < atual.AndamentoEm
            && r.Status == StatusProtocolo.Reprovado);
}
