using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class ProtocoloRepository(DispatchDbContext dbContext) : IProtocoloRepository
{
    public void Adicionar(Protocolo protocolo) => dbContext.Protocolos.Add(protocolo);

    public async Task<Protocolo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Protocolos.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterPorNumerosAsync(IReadOnlyCollection<string> numeros, CancellationToken cancellationToken) =>
        await dbContext.Protocolos.Where(p => numeros.Contains(p.Numero)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterVariosPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Protocolos.Where(p => ids.Contains(p.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterAtribuidosAAsync(Guid conferenteId, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status == StatusProtocolo.Atribuido && p.DonoId == conferenteId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterParaDistribuicaoAsync(Guid? loteImportacaoId, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status != StatusProtocolo.Excluido && (loteImportacaoId == null || p.LoteImportacaoId == loteImportacaoId))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterParaVisaoDistribuicaoAsync(
        Guid? loteImportacaoId, DateTimeOffset concluidosDesde, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status != StatusProtocolo.Excluido && p.Status != StatusProtocolo.Descartado)
            .Where(p => loteImportacaoId == null || p.LoteImportacaoId == loteImportacaoId)
            .Where(p => loteImportacaoId != null
                || p.Status != StatusProtocolo.Aprovado && p.Status != StatusProtocolo.Reprovado
                || p.ConcluidoEm >= concluidosDesde)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterSemDonoAsync(CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status == StatusProtocolo.Pool || p.Status == StatusProtocolo.Excecao)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterAbertosPorEscreventesAsync(
        IReadOnlyCollection<Guid> escreventeIds, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => escreventeIds.Contains(p.EscreventeId) && p.Status != StatusProtocolo.Aprovado
                && p.Status != StatusProtocolo.Reprovado && p.Status != StatusProtocolo.Descartado
                && p.Status != StatusProtocolo.Excluido)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterPoolAsync(CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status == StatusProtocolo.Pool)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterEmConferenciaPorConferenteAsync(
        Guid conferenteId, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status == StatusProtocolo.Conferindo && p.DonoId == conferenteId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterConcluidosPorConferenteAsync(
        Guid conferenteId, DateTimeOffset desde, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.DonoId == conferenteId
                && (p.Status == StatusProtocolo.Aprovado || p.Status == StatusProtocolo.Reprovado)
                && p.ConcluidoEm >= desde)
            .ToListAsync(cancellationToken);

    public async Task<bool> ExisteComTipoAtoAsync(Guid tipoAtoId, CancellationToken cancellationToken) =>
        await dbContext.Protocolos.AnyAsync(p => p.TipoAtoId == tipoAtoId, cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterConcluidosNoPeriodoAsync(
        DateTimeOffset desde, DateTimeOffset ate, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => (p.Status == StatusProtocolo.Aprovado || p.Status == StatusProtocolo.Reprovado)
                && p.ConcluidoEm >= desde && p.ConcluidoEm < ate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<(Guid RegraAlcadaId, int Total)>> ContarPorRegraAplicadaAsync(
        CancellationToken cancellationToken)
    {
        var contagens = await dbContext.Protocolos
            .Where(p => p.RegraAplicadaId != null)
            .GroupBy(p => p.RegraAplicadaId!.Value)
            .Select(g => new { RegraAlcadaId = g.Key, Total = g.Count() })
            .ToListAsync(cancellationToken);

        return contagens.Select(c => (c.RegraAlcadaId, c.Total)).ToList();
    }

    public async Task<IReadOnlyCollection<Guid>> ObterTipoAtoIdsDistintosAsync(CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.TipoAtoId != null)
            .Select(p => p.TipoAtoId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
}
