using Dispatch.Domain;

namespace Dispatch.Application;

public interface ITipoAtoRepository
{
    Task<IReadOnlyCollection<TipoAto>> ObterTodosAsync(CancellationToken cancellationToken);

    // RF-40 ("classifica o tipo"): primeira escrita nesse catálogo — até aqui só existia
    // leitura, tipo desconhecido era só sinalizado (RF-09), nunca cadastrado sozinho.
    void Adicionar(TipoAto tipoAto);
}
