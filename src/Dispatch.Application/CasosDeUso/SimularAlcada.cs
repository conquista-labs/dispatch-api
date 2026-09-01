using Dispatch.Domain;

namespace Dispatch.Application;

// Base do "Testar" da aba Alçada (protótipo v2) — mesma resolução de ObterDetalheProtocolo,
// mas sobre um caso hipotético (etapa/tipo/equipe escolhidos na hora), não um Protocolo real
// já existente. Reaproveita ResolvedorAlcada.Explicar, nenhuma regra nova.
public sealed class SimularAlcada(
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto)
{
    public async Task<ResultadoSimulacaoAlcada?> ExecutarAsync(
        Etapa etapa, Guid tipoAtoId, Guid? equipeId, CancellationToken cancellationToken = default)
    {
        var tipo = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipo is null)
        {
            return null;
        }

        var conferentesNaEscala = await conferentes.ObterNaEscalaAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var caso = new CasoAlcada(etapa, tipo, equipeId);

        var avaliacoes = conferentesNaEscala
            .Select(c => new AvaliacaoCandidatoComTrilha(
                c, ResolvedorAlcada.Resolver(c, caso, regrasAtivas), ResolvedorAlcada.Explicar(c, caso, regrasAtivas)))
            .ToList();

        return new ResultadoSimulacaoAlcada(avaliacoes);
    }
}

public sealed record ResultadoSimulacaoAlcada(IReadOnlyList<AvaliacaoCandidatoComTrilha> Avaliacoes);
