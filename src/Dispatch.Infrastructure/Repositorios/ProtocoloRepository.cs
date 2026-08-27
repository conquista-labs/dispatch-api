using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class ProtocoloRepository(DispatchDbContext dbContext) : IProtocoloRepository
{
    public void Adicionar(Protocolo protocolo) => dbContext.Protocolos.Add(protocolo);

    public async Task<Protocolo?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Protocolos.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterAtribuidosAAsync(Guid conferenteId, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status == StatusProtocolo.Atribuido && p.DonoId == conferenteId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterParaDistribuicaoAsync(Guid? loteImportacaoId, CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => loteImportacaoId == null || p.LoteImportacaoId == loteImportacaoId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Protocolo>> ObterSemDonoAsync(CancellationToken cancellationToken) =>
        await dbContext.Protocolos
            .Where(p => p.Status == StatusProtocolo.Pool || p.Status == StatusProtocolo.Excecao)
            .ToListAsync(cancellationToken);
}
