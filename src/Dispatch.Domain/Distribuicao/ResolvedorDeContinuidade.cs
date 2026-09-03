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
}
