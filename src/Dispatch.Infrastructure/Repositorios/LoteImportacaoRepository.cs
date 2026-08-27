using Dispatch.Application;
using Dispatch.Domain;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class LoteImportacaoRepository(DispatchDbContext dbContext) : ILoteImportacaoRepository
{
    public void Adicionar(LoteImportacao lote) => dbContext.LotesImportacao.Add(lote);
}
