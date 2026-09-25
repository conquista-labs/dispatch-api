using Dispatch.Domain;

namespace Dispatch.Application;

public sealed class ObterVisaoDistribuicao(IProtocoloRepository protocolos, IRelogio relogio)
{
    // Só concluídos (Aprovado/Reprovado) sofrem corte por data nesta tela — os outros buckets
    // são trabalho em andamento, ficam pequenos por natureza. Sem isso, GET
    // /protocolos/distribuicao cresce sem limite pra sempre (achado numa auditoria de
    // performance — ver docs/decisions/0026-corte-de-30-dias-nos-concluidos.md).
    public const int DiasHistoricoDeConcluidos = 30;

    public async Task<VisaoDistribuicao> ExecutarAsync(Guid? loteImportacaoId, CancellationToken cancellationToken = default)
    {
        var concluidosDesde = relogio.Agora.AddDays(-DiasHistoricoDeConcluidos);
        var todos = await protocolos.ObterParaVisaoDistribuicaoAsync(loteImportacaoId, concluidosDesde, cancellationToken);

        // Quem tá vencendo primeiro fica no topo — sem isso a ordem é a do banco, que não é
        // garantida sem ORDER BY (mesma armadilha já documentada em ListarConferentes).
        var pool = todos.Where(p => p.Status == StatusProtocolo.Pool).OrderBy(p => p.VencimentoEm ?? DateTimeOffset.MaxValue).ToList();
        var atribuidos = todos.Where(p => p.Status == StatusProtocolo.Atribuido).ToList();
        var emConferencia = todos.Where(p => p.Status == StatusProtocolo.Conferindo).ToList();
        var concluidos = todos.Where(p => p.Status is StatusProtocolo.Aprovado or StatusProtocolo.Reprovado).ToList();
        var excecoes = todos.Where(p => p.Status == StatusProtocolo.Excecao).ToList();

        var porConferente = atribuidos.Concat(emConferencia)
            .Where(p => p.DonoId is not null)
            .GroupBy(p => p.DonoId!.Value)
            .Select(grupo => new GrupoPorConferente(grupo.Key, grupo.ToList()))
            .ToList();

        // "N feitos hoje" (card de conferente) — "hoje" é local a este caso de uso, mesma
        // decisão de ObterConcluidosHoje (Minha fila): só ele decide o que "início do dia"
        // significa, via IRelogio.
        var inicioDoDia = new DateTimeOffset(relogio.Agora.Date, relogio.Agora.Offset);
        var concluidosHojePorConferente = concluidos
            .Where(p => p.DonoId is not null && p.ConcluidoEm >= inicioDoDia)
            .GroupBy(p => p.DonoId!.Value)
            .Select(grupo => new ConcluidosHojeDoConferente(grupo.Key, grupo.Count()))
            .ToList();

        var numeroDaConferencia = await NumeroDaConferenciaEmLote.CalcularAsync(protocolos, todos, cancellationToken);

        return new VisaoDistribuicao(
            pool, atribuidos, emConferencia, concluidos, excecoes, porConferente, concluidosHojePorConferente, numeroDaConferencia);
    }
}
