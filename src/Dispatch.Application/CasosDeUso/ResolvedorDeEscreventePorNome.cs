using Dispatch.Domain;

namespace Dispatch.Application;

// Busca por nome (case-insensitive) e cria sem equipe se for a primeira vez — mesma lógica que
// já existia inline no handler do (removido) POST /protocolos/distribuir, extraída pra
// reaproveitar sem duplicar nos casos de uso de protocolo manual (RF-18f/g). Não é a mesma
// coisa da resolução em lote de ImportarLote (que resolve contra uma lista já carregada pro
// lote inteiro, com múltiplos escreventes novos acumulados até persistir todos juntos no fim)
// — aqui é sempre um escrevente só, resolvido e persistido na hora. `internal`, mesmo padrão
// dos outros helpers de CasosDeUso/ — só chamado de dentro de Dispatch.Application agora que o
// endpoint avulso que chamava direto foi removido (auditoria de qualidade, zero consumidor).
internal static class ResolvedorDeEscreventePorNome
{
    // "Adicionar" controla se o escrevente novo é registrado no repositório (fluxo real, RF-18f)
    // ou só construído em memória pra simular sem efeito colateral nenhum (RF-18f, prévia).
    public static async Task<Escrevente> ResolverAsync(
        string nome, IEscreventeRepository escreventes, bool adicionarSeNovo, CancellationToken cancellationToken)
    {
        var escrevente = (await escreventes.ObterTodosAsync(cancellationToken))
            .FirstOrDefault(e => string.Equals(e.Nome, nome, StringComparison.OrdinalIgnoreCase));

        if (escrevente is not null)
        {
            return escrevente;
        }

        escrevente = new Escrevente(Guid.NewGuid(), NormalizadorDeTexto.ParaNomeProprio(nome), equipeId: null);
        if (adicionarSeNovo)
        {
            escreventes.Adicionar(escrevente);
        }

        return escrevente;
    }
}
