using Dispatch.Domain;

namespace Dispatch.Application;

// Cadastro manual de escrevente — até aqui, um Escrevente só nascia como efeito colateral de
// importar um lote (RF-09) ou de criar/editar um protocolo manual com nome novo
// (ResolvedorDeEscreventePorNome). Pedido do dono: um jeito deliberado de cadastrar um
// escrevente sozinho, sem precisar de nenhum protocolo por trás. Diferente do resolvedor (que
// silenciosamente reaproveita um escrevente já existente pelo nome), aqui é ação explícita —
// nome duplicado é 409, não reaproveitamento silencioso (mesmo padrão de CriarTipoAto).
public sealed class CriarEscrevente(
    IEscreventeRepository escreventes,
    IEquipeRepository equipes,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoCriarEscrevente> ExecutarAsync(
        string nome, Guid? equipeId, CancellationToken cancellationToken = default)
    {
        var nomeNormalizado = NormalizadorDeTexto.ParaNomeProprio(nome);
        var jaExiste = (await escreventes.ObterTodosAsync(cancellationToken))
            .Any(e => string.Equals(e.Nome, nomeNormalizado, StringComparison.OrdinalIgnoreCase));
        if (jaExiste)
        {
            return new ResultadoCriarEscrevente.JaExiste();
        }

        if (equipeId is { } idInformado && await equipes.ObterPorIdAsync(idInformado, cancellationToken) is null)
        {
            return new ResultadoCriarEscrevente.EquipeNaoEncontrada();
        }

        var escrevente = new Escrevente(Guid.NewGuid(), nomeNormalizado, equipeId);
        escreventes.Adicionar(escrevente);
        await unitOfWork.SalvarAsync(cancellationToken);

        return new ResultadoCriarEscrevente.Sucesso(escrevente.Id);
    }
}

public abstract record ResultadoCriarEscrevente
{
    private ResultadoCriarEscrevente() { }
    public sealed record Sucesso(Guid EscreventeId) : ResultadoCriarEscrevente;
    public sealed record JaExiste : ResultadoCriarEscrevente;
    public sealed record EquipeNaoEncontrada : ResultadoCriarEscrevente;
}
