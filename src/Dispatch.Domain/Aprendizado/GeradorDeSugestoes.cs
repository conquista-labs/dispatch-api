namespace Dispatch.Domain;

// Seção 7: "o sistema que aprende é contagem, não modelo" — as quatro propostas da tabela,
// cada uma pura função de dados já existentes (nenhuma delas precisa de um log de eventos
// solto, ver CLAUDE.md). Limiares têm default igual ao documento; expostos como parâmetro
// porque, assim como as faixas do semáforo, são configuração (tabela `config`, seção 8, ainda
// não existe).
public static class GeradorDeSugestoes
{
    private static readonly IReadOnlyDictionary<TipoPrazo, TimeSpan> DuracaoTipica = new Dictionary<TipoPrazo, TimeSpan>
    {
        [TipoPrazo.UmaHora] = TimeSpan.FromHours(1),
        [TipoPrazo.D0] = TimeSpan.FromHours(12),
        [TipoPrazo.D1] = TimeSpan.FromHours(36),
        [TipoPrazo.D2] = TimeSpan.FromHours(60)
    };

    // "Tipo desconhecido": tipo fora do catálogo com ≥5 ocorrências resolvidas na mão (RF-17
    // — só sai de Exceção com DonoId preenchido). Sugestão: a moda do nível de quem resolveu.
    public static IReadOnlyList<CandidatoSugestao> TipoDesconhecido(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<Conferente> conferentes, int limiar = 5)
    {
        var resolvidosNaMao = protocolos
            .Where(p => p.TipoAtoId is null && p.TipoAtoNomeOriginal is not null && p.DonoId is not null)
            .ToList();

        return resolvidosNaMao
            .GroupBy(p => p.TipoAtoNomeOriginal!, StringComparer.OrdinalIgnoreCase)
            .Where(grupo => grupo.Count() >= limiar)
            .Select(grupo =>
            {
                var nomeTipo = grupo.First().TipoAtoNomeOriginal!;
                var niveis = grupo
                    .Select(p => conferentes.SingleOrDefault(c => c.Id == p.DonoId)?.Nivel)
                    .Where(n => n is not null)
                    .Select(n => n!.Value)
                    .ToList();
                var (nivelSugerido, forcaDaModa) = ModaComForca(niveis);

                return new CandidatoSugestao(
                    $"tipo-desconhecido:{nomeTipo.Trim().ToUpperInvariant()}",
                    new PayloadSugestao.TipoDesconhecido(nomeTipo, nivelSugerido),
                    $"{grupo.Count()} ocorrências de \"{nomeTipo}\" fora do catálogo, resolvidas na mão — nível mais comum: {nivelSugerido}.",
                    grupo.Count(),
                    forcaDaModa);
            })
            .ToList();
    }

    // "Prazo irreal": ≥8 casos concluídos e >60% de estouro em equipe+etapa. Sugestão: a faixa
    // mais próxima do percentil 80 da duração real (AndamentoEm → ConcluidoEm).
    public static IReadOnlyList<CandidatoSugestao> PrazoIrreal(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<Escrevente> escreventes,
        int limiarCasos = 8, double limiarEstouro = 0.6)
    {
        var concluidos = protocolos
            .Where(p => p.Status is StatusProtocolo.Aprovado or StatusProtocolo.Reprovado && p.VencimentoEm is not null && p.ConcluidoEm is not null)
            .Select(p => (Protocolo: p, EquipeId: escreventes.SingleOrDefault(e => e.Id == p.EscreventeId)?.EquipeId))
            .Where(par => par.EquipeId is not null)
            .ToList();

        return concluidos
            .GroupBy(par => (EquipeId: par.EquipeId!.Value, par.Protocolo.Etapa))
            .Where(grupo => grupo.Count() >= limiarCasos)
            .Select(grupo =>
            {
                var casos = grupo.Count();
                var estourados = grupo.Count(par => par.Protocolo.ConcluidoEm > par.Protocolo.VencimentoEm);
                return (grupo.Key, casos, percentualEstouro: (double)estourados / casos, grupo);
            })
            .Where(t => t.percentualEstouro > limiarEstouro)
            .Select(t =>
            {
                var duracoes = t.grupo.Select(par => par.Protocolo.ConcluidoEm!.Value - par.Protocolo.AndamentoEm).ToList();
                var percentil80 = Percentil80(duracoes);
                var prazoSugerido = FaixaMaisProxima(percentil80);

                return new CandidatoSugestao(
                    $"prazo-irreal:{t.Key.EquipeId}:{t.Key.Etapa}",
                    new PayloadSugestao.PrazoIrreal(t.Key.EquipeId, t.Key.Etapa, prazoSugerido),
                    $"{t.casos} casos, {t.percentualEstouro:P0} estourando o prazo em {t.Key.Etapa} — " +
                    $"percentil 80 da duração real: {percentil80.TotalHours:F1}h, faixa sugerida: {prazoSugerido}.",
                    t.casos,
                    t.percentualEstouro);
            })
            .ToList();
    }

    // "Escrevente órfão": ≥3 protocolos sem equipe. Sugestão: a equipe dominante entre os
    // outros escreventes que apareceram no(s) mesmo(s) lote(s) — indício de que é do mesmo
    // andar/turma que os demais daquele relatório.
    public static IReadOnlyList<CandidatoSugestao> EscreventeOrfao(
        IReadOnlyCollection<Escrevente> escreventes, IReadOnlyCollection<Protocolo> protocolos, int limiar = 3)
    {
        var candidatos = new List<CandidatoSugestao>();

        foreach (var orfao in escreventes.Where(e => e.EquipeId is null))
        {
            var protocolosDoOrfao = protocolos.Where(p => p.EscreventeId == orfao.Id).ToList();
            if (protocolosDoOrfao.Count < limiar)
            {
                continue;
            }

            var lotesDoOrfao = protocolosDoOrfao.Select(p => p.LoteImportacaoId).Where(id => id is not null).Distinct().ToList();
            var equipesNoMesmoLote = protocolos
                .Where(p => lotesDoOrfao.Contains(p.LoteImportacaoId) && p.EscreventeId != orfao.Id)
                .Select(p => escreventes.SingleOrDefault(e => e.Id == p.EscreventeId)?.EquipeId)
                .Where(equipeId => equipeId is not null)
                .Select(equipeId => equipeId!.Value)
                .ToList();

            if (equipesNoMesmoLote.Count == 0)
            {
                continue;
            }

            var (equipeSugerida, dominanciaDaEquipe) = ModaGuidComForca(equipesNoMesmoLote);
            candidatos.Add(new CandidatoSugestao(
                $"escrevente-orfao:{orfao.Id}",
                new PayloadSugestao.EscreventeOrfao(orfao.Id, equipeSugerida),
                $"{protocolosDoOrfao.Count} protocolos de \"{orfao.Nome}\" sem equipe — equipe dominante no(s) mesmo(s) lote(s).",
                protocolosDoOrfao.Count,
                dominanciaDaEquipe));
        }

        return candidatos;
    }

    // "Risco de qualidade": ≥6 casos e >50% de reprovação em tipo+nível. Sugestão: negar
    // aquele nível pra aquele tipo (restringe ao nível acima). Sênior não gera sugestão — não
    // existe nível acima pra restringir.
    public static IReadOnlyList<CandidatoSugestao> RiscoQualidade(
        IReadOnlyCollection<Protocolo> protocolos, IReadOnlyCollection<Conferente> conferentes,
        int limiarCasos = 6, double limiarReprovacao = 0.5)
    {
        var concluidos = protocolos
            .Where(p => p.Status is StatusProtocolo.Aprovado or StatusProtocolo.Reprovado && p.TipoAtoId is not null && p.DonoId is not null)
            .Select(p => (Protocolo: p, Nivel: conferentes.SingleOrDefault(c => c.Id == p.DonoId)?.Nivel))
            .Where(par => par.Nivel is not null)
            .ToList();

        return concluidos
            .GroupBy(par => (TipoAtoId: par.Protocolo.TipoAtoId!.Value, Nivel: par.Nivel!.Value))
            .Where(grupo => grupo.Key.Nivel != Nivel.Senior && grupo.Count() >= limiarCasos)
            .Select(grupo =>
            {
                var casos = grupo.Count();
                var reprovados = grupo.Count(par => par.Protocolo.Status == StatusProtocolo.Reprovado);
                return (grupo.Key, casos, percentualReprovacao: (double)reprovados / casos);
            })
            .Where(t => t.percentualReprovacao > limiarReprovacao)
            .Select(t => new CandidatoSugestao(
                $"risco-qualidade:{t.Key.TipoAtoId}:{t.Key.Nivel}",
                new PayloadSugestao.RiscoQualidade(t.Key.TipoAtoId, t.Key.Nivel),
                $"{t.casos} casos de nível {t.Key.Nivel}, {t.percentualReprovacao:P0} reprovados — sugerido restringir ao nível acima.",
                t.casos,
                t.percentualReprovacao))
            .ToList();
    }

    // Além do valor mais comum, devolve a força dele (contagem do grupo majoritário / total) —
    // é o sinal de confiança das sugestões TipoDesconhecido/EscreventeOrfao (ver
    // CandidatoSugestao.IndiceConfianca).
    private static (Nivel Valor, double Forca) ModaComForca(IReadOnlyCollection<Nivel> valores)
    {
        var grupo = valores.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
        return (grupo.Key, (double)grupo.Count() / valores.Count);
    }

    private static (Guid Valor, double Forca) ModaGuidComForca(IReadOnlyCollection<Guid> valores)
    {
        var grupo = valores.GroupBy(v => v).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First();
        return (grupo.Key, (double)grupo.Count() / valores.Count);
    }

    private static TimeSpan Percentil80(IReadOnlyList<TimeSpan> duracoes)
    {
        var ordenadas = duracoes.OrderBy(d => d).ToList();
        var indice = (int)Math.Ceiling(0.8 * (ordenadas.Count - 1));
        return ordenadas[indice];
    }

    private static TipoPrazo FaixaMaisProxima(TimeSpan duracaoReal) =>
        DuracaoTipica.OrderBy(par => Math.Abs((par.Value - duracaoReal).Ticks)).First().Key;
}
