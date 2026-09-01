namespace Dispatch.Application;

// RF-34a: leitura agregada pra tabela da aba "Tipos de ato" — volume (quantos protocolos já
// usaram esse tipo) e quantos conferentes na escala têm alçada pra ele hoje. Reaproveita
// ObterAlcancePorConferente em vez de rodar ResolvedorAlcada de novo, mesmo padrão já usado
// por ObterCoberturaDeAlcada (RF-30).
public sealed class ListarTiposAtoComUso(
    ITipoAtoRepository tiposAto,
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    ObterAlcancePorConferente obterAlcance)
{
    public async Task<IReadOnlyList<TipoAtoComUso>> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var catalogo = await tiposAto.ObterTodosAsync(cancellationToken);
        var todosOsProtocolos = await protocolos.ObterParaDistribuicaoAsync(loteImportacaoId: null, cancellationToken);
        var volumePorTipoId = todosOsProtocolos
            .Where(p => p.TipoAtoId is not null)
            .GroupBy(p => p.TipoAtoId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var naEscalaIds = (await conferentes.ObterNaEscalaAsync(cancellationToken)).Select(c => c.Id).ToHashSet();
        var alcancePorConferente = await obterAlcance.ExecutarAsync(cancellationToken);

        return catalogo
            .Select(tipo =>
            {
                var comAlcada = alcancePorConferente.Count(a => naEscalaIds.Contains(a.ConferenteId) && a.TiposPermitidosIds.Contains(tipo.Id));
                return new TipoAtoComUso(
                    tipo.Id, tipo.Nome, tipo.Ativo, tipo.PesoComplexidade, tipo.Grupo,
                    volumePorTipoId.GetValueOrDefault(tipo.Id), comAlcada);
            })
            .OrderBy(t => t.Nome)
            .ToList();
    }
}

public sealed record TipoAtoComUso(
    Guid Id, string Nome, bool Ativo, int PesoComplexidade, Dispatch.Domain.GrupoTipoAto? Grupo, int Volume, int ConferentesComAlcada);
