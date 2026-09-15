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
    public const int TamanhoDePaginaPadrao = 20;
    private const int TamanhoDePaginaMaximo = 100;

    // Pedido do dono depois de ver o catálogo real crescer (~24 tipos e subindo): busca +
    // rolagem contida (mitigação client-side, usada em todo o resto do app) não bastava mais
    // aqui — primeira paginação de verdade do sistema. Busca filtra por nome antes de paginar,
    // senão "página 2" nunca bateria com o que a busca do usuário esperava ver.
    public async Task<Paginado<TipoAtoComUso>> ExecutarAsync(
        string? busca = null, int pagina = 1, int tamanhoPagina = TamanhoDePaginaPadrao, CancellationToken cancellationToken = default)
    {
        var catalogo = await tiposAto.ObterTodosAsync(cancellationToken);
        var filtrado = string.IsNullOrWhiteSpace(busca)
            ? catalogo
            : catalogo.Where(t => t.Nome.Contains(busca, StringComparison.OrdinalIgnoreCase)).ToList();

        var todosOsProtocolos = await protocolos.ObterParaDistribuicaoAsync(loteImportacaoId: null, cancellationToken);
        var volumePorTipoId = todosOsProtocolos
            .Where(p => p.TipoAtoId is not null)
            .GroupBy(p => p.TipoAtoId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var naEscalaIds = (await conferentes.ObterNaEscalaAsync(cancellationToken)).Select(c => c.Id).ToHashSet();
        var alcancePorConferente = await obterAlcance.ExecutarAsync(cancellationToken);

        var todos = filtrado
            .Select(tipo =>
            {
                var comAlcada = alcancePorConferente.Count(a => naEscalaIds.Contains(a.ConferenteId) && a.TiposPermitidosIds.Contains(tipo.Id));
                return new TipoAtoComUso(
                    tipo.Id, tipo.Nome, tipo.Ativo, tipo.PesoComplexidade, tipo.Grupo,
                    volumePorTipoId.GetValueOrDefault(tipo.Id), comAlcada);
            })
            .OrderBy(t => t.Nome)
            .ToList();

        var paginaValida = Math.Max(1, pagina);
        var tamanhoValido = Math.Clamp(tamanhoPagina, 1, TamanhoDePaginaMaximo);
        var itensDaPagina = todos.Skip((paginaValida - 1) * tamanhoValido).Take(tamanhoValido).ToList();

        return new Paginado<TipoAtoComUso>(itensDaPagina, todos.Count);
    }
}

public sealed record TipoAtoComUso(
    Guid Id, string Nome, bool Ativo, int PesoComplexidade, Dispatch.Domain.GrupoTipoAto? Grupo, int Volume, int ConferentesComAlcada);
