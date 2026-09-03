using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

// Linha única, semeada por migration — SingleAsync() (não SingleOrDefaultAsync) porque a
// ausência de linha é erro de setup, não um caso de negócio a tratar.
public sealed class ConfiguracaoRepository(DispatchDbContext dbContext) : IConfiguracaoRepository
{
    public async Task<Configuracao> ObterAsync(CancellationToken cancellationToken) =>
        await dbContext.Configuracoes.SingleAsync(cancellationToken);
}
