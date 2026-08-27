using Dispatch.Domain;

namespace Dispatch.Application;

public interface ILoteImportacaoRepository
{
    void Adicionar(LoteImportacao lote);
}
