using Dispatch.Domain;

namespace Dispatch.Application;

// RF-31. Quem monta a frase legível ("quem → pode/não pode → tal coisa") é a Api, traduzindo
// o request pros tipos ricos do Domain — aqui só valida que sujeito/alvo por pessoa/tipo
// apontam pra algo que existe de verdade, e cria a regra (sempre Origem.Manual — Aprendida
// nasce só do módulo de aprendizado, que não existe ainda).
public sealed class CriarRegraAlcada(
    IRegraAlcadaRepository regras,
    IConferenteRepository conferentes,
    ITipoAtoRepository tiposAto,
    IEquipeRepository equipes,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoCriarRegraAlcada> ExecutarAsync(
        SujeitoAlcada sujeito, PermissaoRegra permissao, AlvoAlcada alvo, CancellationToken cancellationToken = default)
    {
        if (sujeito is SujeitoAlcada.PorPessoa porPessoa)
        {
            var conferente = await conferentes.ObterPorIdAsync(porPessoa.ConferenteId, cancellationToken);
            if (conferente is null)
            {
                return new ResultadoCriarRegraAlcada.ConferenteNaoEncontrado();
            }
        }

        if (alvo is AlvoAlcada.PorTipoAto porTipo)
        {
            var catalogo = await tiposAto.ObterTodosAsync(cancellationToken);
            if (!catalogo.Any(t => t.Id == porTipo.TipoAtoId))
            {
                return new ResultadoCriarRegraAlcada.TipoAtoNaoEncontrado();
            }
        }

        // Guid? nulo em PorEquipeDeEscrevente é "sem equipe" — alvo válido, não precisa
        // validar referência nenhuma (RF-29a).
        if (alvo is AlvoAlcada.PorEquipeDeEscrevente { EquipeId: { } equipeId })
        {
            var equipe = await equipes.ObterPorIdAsync(equipeId, cancellationToken);
            if (equipe is null)
            {
                return new ResultadoCriarRegraAlcada.EquipeNaoEncontrada();
            }
        }

        var regra = new RegraAlcada(Guid.NewGuid(), sujeito, permissao, alvo);
        regras.Adicionar(regra);
        await unitOfWork.SalvarAsync(cancellationToken);

        return new ResultadoCriarRegraAlcada.Sucesso(regra.Id);
    }
}

public abstract record ResultadoCriarRegraAlcada
{
    private ResultadoCriarRegraAlcada() { }

    public sealed record Sucesso(Guid RegraId) : ResultadoCriarRegraAlcada;

    public sealed record ConferenteNaoEncontrado : ResultadoCriarRegraAlcada;

    public sealed record TipoAtoNaoEncontrado : ResultadoCriarRegraAlcada;

    public sealed record EquipeNaoEncontrada : ResultadoCriarRegraAlcada;
}
