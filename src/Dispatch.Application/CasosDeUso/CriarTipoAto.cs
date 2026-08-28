using Dispatch.Domain;

namespace Dispatch.Application;

// Cadastro manual de tipo de ato na Central de Regras — complementa o cadastro automático que
// a importação já faz (ImportarLote): a Distribuidora pode querer registrar um tipo antes dele
// aparecer num relatório, ou como parte de organizar o catálogo (RF-31, painel de alçada precisa
// de um alvo pra apontar a regra).
public sealed class CriarTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoCriarTipoAto> ExecutarAsync(string nome, CancellationToken cancellationToken = default)
    {
        var nomeNormalizado = NormalizadorDeTexto.ParaNomeProprio(nome);
        var existentes = await tiposAto.ObterTodosAsync(cancellationToken);
        if (existentes.Any(t => string.Equals(t.Nome, nomeNormalizado, StringComparison.OrdinalIgnoreCase)))
        {
            return new ResultadoCriarTipoAto.JaExiste();
        }

        var tipoAto = new TipoAto(Guid.NewGuid(), nomeNormalizado);
        tiposAto.Adicionar(tipoAto);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoCriarTipoAto.Sucesso(tipoAto.Id);
    }
}

public abstract record ResultadoCriarTipoAto
{
    private ResultadoCriarTipoAto() { }

    public sealed record Sucesso(Guid TipoAtoId) : ResultadoCriarTipoAto;

    public sealed record JaExiste : ResultadoCriarTipoAto;
}
