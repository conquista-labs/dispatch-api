namespace Dispatch.Application;

// RF-30: aviso de cobertura na tela Conferentes — "tipo em circulação" é qualquer TipoAto que
// aparece nos protocolos de hoje (não o catálogo inteiro; um tipo sem protocolo nenhum não é
// um problema de cobertura agora). Reaproveita ObterAlcancePorConferente em vez de rodar
// ResolvedorAlcada de novo — TiposPermitidosIds já resolve "esse conferente alcança esse tipo
// em pelo menos uma etapa" (Pré ou Pós, sem equipe; ver a aproximação documentada lá). Quem só
// faz uma das etapas conta como habilitado — antes a lista só olhava Pós e quem só fazia pré
// virava falso "ninguém habilitado".
public sealed class ObterCoberturaDeAlcada(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    ObterAlcancePorConferente obterAlcance,
    ITipoAtoRepository tiposAto)
{
    public async Task<CoberturaAlcada> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        // SELECT DISTINCT no banco em vez de carregar a tabela protocolos inteira pra memória
        // só pra extrair os tipos em jogo (achado numa auditoria de performance).
        var tiposEmJogoIds = await protocolos.ObterTipoAtoIdsDistintosAsync(cancellationToken);

        if (tiposEmJogoIds.Count == 0)
        {
            return new CoberturaAlcada([], []);
        }

        var conferentesNaEscalaIds = (await conferentes.ObterNaEscalaAsync(cancellationToken))
            .Select(c => c.Id)
            .ToHashSet();
        var alcancePorConferente = await obterAlcance.ExecutarAsync(cancellationToken);
        var nomePorTipoId = (await tiposAto.ObterTodosAsync(cancellationToken)).ToDictionary(t => t.Id, t => t.Nome);

        var semNinguemHabilitado = new List<TipoDeAtoResumo>();
        var dependeDeUmaPessoa = new List<TipoDeAtoResumo>();

        foreach (var tipoId in tiposEmJogoIds)
        {
            // Tipo fora do catálogo (RF-09, "tipo desconhecido") já é sinalizado na importação —
            // não é falta de cobertura de alçada, é falta de cadastro do tipo em si.
            if (!nomePorTipoId.TryGetValue(tipoId, out var nome))
            {
                continue;
            }

            var habilitados = alcancePorConferente.Count(a => conferentesNaEscalaIds.Contains(a.ConferenteId) && a.TiposPermitidosIds.Contains(tipoId));

            if (habilitados == 0)
            {
                semNinguemHabilitado.Add(new TipoDeAtoResumo(tipoId, nome));
            }
            else if (habilitados == 1)
            {
                dependeDeUmaPessoa.Add(new TipoDeAtoResumo(tipoId, nome));
            }
        }

        return new CoberturaAlcada(semNinguemHabilitado, dependeDeUmaPessoa);
    }
}

public sealed record CoberturaAlcada(
    IReadOnlyList<TipoDeAtoResumo> SemNinguemHabilitado,
    IReadOnlyList<TipoDeAtoResumo> DependeDeUmaPessoa);

public sealed record TipoDeAtoResumo(Guid TipoAtoId, string Nome);
