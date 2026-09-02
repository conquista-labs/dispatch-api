using Dispatch.Domain;

namespace Dispatch.Application;

// Base do "Testar" da aba Alçada (protótipo v2) — mesma resolução de ObterDetalheProtocolo,
// mas sobre um caso hipotético (etapa/tipo/equipe/prioridade escolhidos na hora), não um
// Protocolo real já existente. A avaliação de elegibilidade por candidato reaproveita
// ResolvedorAlcada.Explicar (nenhuma regra nova); o destino (RF-34, "o que aconteceria de
// verdade") roda o motor de distribuição de verdade (AplicadorDeDistribuicao, mesma técnica de
// SimularProtocoloManual — Protocolo/Escrevente transitórios, nunca persistidos) — achado numa
// auditoria de qualidade que o destino não podia ser inferido só pela contagem de elegíveis no
// front (a regra real decide primeiro por urgência, não por contagem).
public sealed class SimularAlcada(
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IEquipeRepository equipes,
    IRelogio relogio)
{
    public async Task<ResultadoSimulacaoAlcada?> ExecutarAsync(
        Etapa etapa, Guid tipoAtoId, Guid? equipeId, Prioridade prioridade, CancellationToken cancellationToken = default)
    {
        var catalogoTipos = await tiposAto.ObterTodosAsync(cancellationToken);
        var tipo = catalogoTipos.FirstOrDefault(t => t.Id == tipoAtoId);
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

        // Sem escrevente de verdade nesse simulador (só etapa/equipe hipotéticas) — o nome não
        // importa pra nada aqui, só o EquipeId (ResolvedorDePrazo.Resolver só lê isso).
        var escreventeHipotetico = new Escrevente(Guid.NewGuid(), "(simulação)", equipeId);
        var protocoloHipotetico = new Protocolo(Guid.NewGuid(), "(simulação)", tipoAtoId, escreventeHipotetico.Id, etapa, relogio.Agora, prioridade);

        var resultadoDistribuicao = AplicadorDeDistribuicao.Executar(
            protocoloHipotetico,
            escreventeHipotetico,
            await equipes.ObterTodasAsync(cancellationToken),
            conferentesNaEscala,
            regrasAtivas,
            catalogoTipos,
            relogio.Agora,
            out _);

        var (destino, conferenteId, motivo) = resultadoDistribuicao switch
        {
            ResultadoDistribuicao.Atribuido atribuido => ("Atribuido", (Guid?)atribuido.Conferente.Id, (string?)null),
            ResultadoDistribuicao.EnviadoParaPool => ("EnviadoParaPool", null, null),
            ResultadoDistribuicao.Excecao excecao => ("Excecao", null, excecao.Motivo),
            _ => throw new InvalidOperationException($"Resultado de distribuição não mapeado: {resultadoDistribuicao.GetType().Name}")
        };

        return new ResultadoSimulacaoAlcada(avaliacoes, destino, conferenteId, motivo);
    }
}

public sealed record ResultadoSimulacaoAlcada(
    IReadOnlyList<AvaliacaoCandidatoComTrilha> Avaliacoes, string Destino, Guid? ConferenteId, string? Motivo);
