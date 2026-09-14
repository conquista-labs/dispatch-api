using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Dispatch.Infrastructure.Repositorios;

// Linha única, semeada por migration — SingleAsync() (não SingleOrDefaultAsync) porque a
// ausência de linha é erro de setup, não um caso de negócio a tratar.
//
// ObterAsync cacheia em memória (IMemoryCache é singleton — sobrevive entre requisições,
// diferente do DbContext, que é scoped) — achado numa auditoria de performance: até 6
// endpoints/casos de uso diferentes liam essa linha do banco a cada request, pra um dado que
// só muda quando alguém edita explicitamente em PUT /config. TTL curto (não "pra sempre") como
// rede de segurança caso a invalidação explícita (AtualizarConfiguracao → InvalidarCache) seja
// esquecida num caminho novo no futuro.
public sealed class ConfiguracaoRepository(DispatchDbContext dbContext, IMemoryCache cache) : IConfiguracaoRepository
{
    private const string ChaveCache = "configuracao";
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public Task<Configuracao> ObterAsync(CancellationToken cancellationToken) =>
        cache.GetOrCreateAsync(ChaveCache, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return await dbContext.Configuracoes.SingleAsync(cancellationToken);
        })!;

    public async Task<Configuracao> ObterParaEdicaoAsync(CancellationToken cancellationToken) =>
        await dbContext.Configuracoes.SingleAsync(cancellationToken);

    public void InvalidarCache() => cache.Remove(ChaveCache);
}
