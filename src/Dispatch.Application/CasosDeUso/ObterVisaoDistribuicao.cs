using Dispatch.Domain;

namespace Dispatch.Application;

public sealed class ObterVisaoDistribuicao(IProtocoloRepository protocolos)
{
    public async Task<VisaoDistribuicao> ExecutarAsync(Guid? loteImportacaoId, CancellationToken cancellationToken = default)
    {
        var todos = await protocolos.ObterParaDistribuicaoAsync(loteImportacaoId, cancellationToken);

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

        return new VisaoDistribuicao(pool, atribuidos, emConferencia, concluidos, excecoes, porConferente);
    }
}
