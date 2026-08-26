namespace Dispatch.Domain;

// Os 5 passos da seção 4 do documento de requisitos.
public static class MotorDistribuicao
{
    public static ResultadoDistribuicao Distribuir(
        Protocolo protocolo,
        IReadOnlyCollection<Conferente> conferentes,
        IReadOnlyCollection<RegraAlcada> regras,
        IReadOnlyCollection<TipoAto> catalogoTipos)
    {
        var tipoConhecido = catalogoTipos.Any(t => t.Id == protocolo.TipoAtoId);
        if (!tipoConhecido)
        {
            return new ResultadoDistribuicao.Excecao("tipo desconhecido", []);
        }

        var candidatosNaEscala = conferentes.Where(c => c.NaEscala).ToList();

        var avaliacoes = candidatosNaEscala
            .Select(c => new AvaliacaoCandidato(
                c,
                DecisaoEtapa: ResolvedorAlcada.Resolver(c, new AlvoAlcada.PorEtapa(protocolo.Etapa), regras),
                DecisaoTipo: ResolvedorAlcada.Resolver(c, new AlvoAlcada.PorTipoAto(protocolo.TipoAtoId), regras)))
            .ToList();

        var elegiveis = avaliacoes.Where(a => a.Elegivel).ToList();
        if (elegiveis.Count == 0)
        {
            return new ResultadoDistribuicao.Excecao("ninguém com alçada", avaliacoes);
        }

        if (!protocolo.Urgente)
        {
            return new ResultadoDistribuicao.EnviadoParaPool(elegiveis);
        }

        var escolhido = elegiveis.OrderBy(a => a.Conferente.CargaAtual).First();
        return new ResultadoDistribuicao.Atribuido(escolhido.Conferente, escolhido);
    }
}
