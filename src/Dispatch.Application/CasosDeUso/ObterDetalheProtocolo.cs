using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18a: painel de detalhe do protocolo — junta o protocolo com "quem pode conferir este ato
// especificamente" (o inverso de GET /conferentes/alcance, que é por conferente). Reaproveita
// ResolvedorAlcada puro, mesma resolução que o motor de distribuição já usa internamente —
// nenhuma regra nova, só reporta o que já existe pro alvo deste protocolo.
public sealed class ObterDetalheProtocolo(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras)
{
    public async Task<ResultadoDetalheProtocolo?> ExecutarAsync(Guid protocoloId, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return null;
        }

        var conferentesNaEscala = await conferentes.ObterNaEscalaAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);

        var avaliacoes = conferentesNaEscala.Select(c => new AvaliacaoCandidato(
                c,
                ResolvedorAlcada.Resolver(c, new AlvoAlcada.PorEtapa(protocolo.Etapa), regrasAtivas),
                // Tipo desconhecido (TipoAtoId nulo) nunca é elegível — não tem alvo pra
                // resolver regra nenhuma contra.
                protocolo.TipoAtoId is { } tipoAtoId
                    ? ResolvedorAlcada.Resolver(c, new AlvoAlcada.PorTipoAto(tipoAtoId), regrasAtivas)
                    : new DecisaoAlcada(ResultadoAlcada.Negado, RegraAplicada: null)))
            .ToList();

        return new ResultadoDetalheProtocolo(protocolo, avaliacoes);
    }
}

public sealed record ResultadoDetalheProtocolo(Protocolo Protocolo, IReadOnlyList<AvaliacaoCandidato> Avaliacoes);
