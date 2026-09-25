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

        // Histórico de conferências (pedido do dono, não é RF numerado): outras linhas com o
        // mesmo Número (RF-07, Numero não é único de propósito) — a mais recente primeiro,
        // mesmo padrão de "quem vence primeiro no topo" já usado no pool.
        var linhasDoNumero = await protocolos.ObterPorNumerosAsync([protocolo.Numero], cancellationToken);
        var historico = linhasDoNumero
            .Where(p => p.Id != protocolo.Id)
            .OrderByDescending(p => p.AndamentoEm)
            .ToList();

        // RF-24k: o nº da conferência do protocolo e de cada linha do histórico, reaproveitando as
        // linhas que já vieram acima (nenhuma query a mais).
        var registros = linhasDoNumero.Select(RegistroDoNumero.De).ToList();
        var numeroDaConferencia = historico.Append(protocolo)
            .ToDictionary(
                p => p.Id,
                p => ResolvedorDeContinuidade.NumeroDaConferencia(RegistroDoNumero.De(p), registros));

        // Tipo desconhecido (TipoAtoId nulo, ou removido do catálogo) nunca é elegível — não
        // tem alvo pra resolver regra nenhuma contra.
        if (tipo is null)
        {
            var negado = new DecisaoAlcada(ResultadoAlcada.Negado, RegraAplicada: null);
            return new ResultadoDetalheProtocolo(
                protocolo, conferentesNaEscala.Select(c => new AvaliacaoCandidatoComTrilha(c, negado, [])).ToList(), historico,
                numeroDaConferencia);
        }

        var caso = new CasoAlcada(protocolo.Etapa, tipo, equipeDoEscreventeId);
        var avaliacoes = conferentesNaEscala
            .Select(c => new AvaliacaoCandidatoComTrilha(
                c, ResolvedorAlcada.Resolver(c, caso, regrasAtivas), ResolvedorAlcada.Explicar(c, caso, regrasAtivas)))
            .ToList();

        return new ResultadoDetalheProtocolo(protocolo, avaliacoes, historico, numeroDaConferencia);
    }
}

// Mesma forma de AvaliacaoCandidato (Domain), com a trilha por camada a mais — só faz sentido
// pra leitura explicativa (painel de detalhe, simulador "Testar"), não pro Domain em si.
public sealed record AvaliacaoCandidatoComTrilha(Conferente Conferente, DecisaoAlcada Decisao, IReadOnlyList<PassoTrilha> Trilha)
{
    public bool Elegivel => Decisao.Resultado == ResultadoAlcada.Permitido;
}

public sealed record ResultadoDetalheProtocolo(
    Protocolo Protocolo,
    IReadOnlyList<AvaliacaoCandidatoComTrilha> Avaliacoes,
    IReadOnlyList<Protocolo> HistoricoConferencias,
    // RF-24k: protocoloId → nº da conferência, do próprio protocolo e de cada linha do histórico.
    IReadOnlyDictionary<Guid, int> NumeroDaConferencia);
