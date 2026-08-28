using Dispatch.Domain;

namespace Dispatch.Application;

public interface ITipoAtoRepository
{
    Task<IReadOnlyCollection<TipoAto>> ObterTodosAsync(CancellationToken cancellationToken);
    Task<TipoAto?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken);

    // RF-40 ("classifica o tipo"): primeira escrita nesse catálogo — até aqui só existia
    // leitura, tipo desconhecido era só sinalizado (RF-09), nunca cadastrado sozinho.
    void Adicionar(TipoAto tipoAto);

    // RF-34e: só chamado depois de confirmar "sem nenhum uso" no caso de uso — ao contrário de
    // Conferente (RemoverConferente é soft delete via Usuario.Desativar), aqui é exclusão de
    // verdade, porque não sobra nada que precise preservar histórico.
    void Remover(TipoAto tipoAto);
}
