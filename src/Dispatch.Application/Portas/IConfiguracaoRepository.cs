using Dispatch.Domain;

namespace Dispatch.Application;

// Linha única — sem Adicionar/ExisteAsync, a linha sempre existe (semeada por migration).
public interface IConfiguracaoRepository
{
    Task<Configuracao> ObterAsync(CancellationToken cancellationToken);
}
