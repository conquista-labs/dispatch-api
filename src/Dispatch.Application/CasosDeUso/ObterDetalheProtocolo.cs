using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18a: painel de detalhe do protocolo — junta o protocolo com "quem pode conferir este ato
// especificamente" (o inverso de GET /conferentes/alcance, que é por conferente). Reaproveita
// ResolvedorAlcada puro, mesma resolução que o motor de distribuição já usa internamente —
// nenhuma regra nova, só reporta o que já existe pro alvo deste protocolo.
public sealed class ObterDetalheProtocolo(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IEscreventeRepository escreventes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto)
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
        var equipeDoEscreventeId = (await escreventes.ObterPorIdAsync(protocolo.EscreventeId, cancellationToken))?.EquipeId;
        var tipo = protocolo.TipoAtoId is { } tipoAtoId ? await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken) : null;

        // Tipo desconhecido (TipoAtoId nulo, ou removido do catálogo) nunca é elegível — não
        // tem alvo pra resolver regra nenhuma contra.
        if (tipo is null)
        {
            var negado = new DecisaoAlcada(ResultadoAlcada.Negado, RegraAplicada: null);
            return new ResultadoDetalheProtocolo(
                protocolo, conferentesNaEscala.Select(c => new AvaliacaoCandidatoComTrilha(c, negado, [])).ToList());
        }

        var caso = new CasoAlcada(protocolo.Etapa, tipo, equipeDoEscreventeId);
        var avaliacoes = conferentesNaEscala
            .Select(c => new AvaliacaoCandidatoComTrilha(
                c, ResolvedorAlcada.Resolver(c, caso, regrasAtivas), ResolvedorAlcada.Explicar(c, caso, regrasAtivas)))
            .ToList();

        return new ResultadoDetalheProtocolo(protocolo, avaliacoes);
    }
}

// Mesma forma de AvaliacaoCandidato (Domain), com a trilha por camada a mais — só faz sentido
// pra leitura explicativa (painel de detalhe, simulador "Testar"), não pro Domain em si.
public sealed record AvaliacaoCandidatoComTrilha(Conferente Conferente, DecisaoAlcada Decisao, IReadOnlyList<PassoTrilha> Trilha)
{
    public bool Elegivel => Decisao.Resultado == ResultadoAlcada.Permitido;
}

public sealed record ResultadoDetalheProtocolo(Protocolo Protocolo, IReadOnlyList<AvaliacaoCandidatoComTrilha> Avaliacoes);
