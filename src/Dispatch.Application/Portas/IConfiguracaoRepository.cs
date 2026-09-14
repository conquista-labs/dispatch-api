using Dispatch.Domain;

namespace Dispatch.Application;

// Linha única — sem Adicionar/ExisteAsync, a linha sempre existe (semeada por migration).
public interface IConfiguracaoRepository
{
    // Leitura cacheada (config quase nunca muda, mas é lida por ~6 endpoints/casos de uso
    // diferentes por request — achado numa auditoria de performance). Usada por todo mundo
    // que só precisa dos valores (faixas do semáforo, limiares do aprendizado, GET /config).
    Task<Configuracao> ObterAsync(CancellationToken cancellationToken);

    // Sempre fresco e rastreado pelo DbContext atual — usado só por AtualizarConfiguracao, que
    // precisa mutar a instância de verdade antes de SaveChanges. Nunca passa pelo cache: um
    // objeto cacheado pode ter vindo de um DbContext de uma requisição anterior já finalizada,
    // e mutar+salvar ele não persistiria nada (mesma armadilha de "objeto desconectado do
    // change tracker" já documentada pra RegraAlcada/Sugestao).
    Task<Configuracao> ObterParaEdicaoAsync(CancellationToken cancellationToken);

    // Chamado depois de um SaveChanges bem-sucedido em AtualizarConfiguracao — sem isso o
    // cache serviria valor velho pros outros consumidores até o TTL expirar sozinho.
    void InvalidarCache();
}
