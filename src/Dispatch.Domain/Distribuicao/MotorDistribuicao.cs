namespace Dispatch.Domain;

// Os 5 passos da seção 4 do documento de requisitos.
public static class MotorDistribuicao
{
    public static ResultadoDistribuicao Distribuir(
        Protocolo protocolo,
        IReadOnlyCollection<Conferente> conferentes,
        IReadOnlyCollection<RegraAlcada> regras,
        IReadOnlyCollection<TipoAto> catalogoTipos,
        Guid? equipeDoEscreventeId = null)
    {
        var tipo = catalogoTipos.FirstOrDefault(t => t.Id == protocolo.TipoAtoId);
        if (tipo is null)
        {
            return new ResultadoDistribuicao.Excecao("tipo desconhecido", []);
        }

        // RF-34d: tipo desativado não apaga histórico, mas os próximos protocolos que
        // chegarem com ele vão para exceção — motivo distinto de "tipo desconhecido" porque a
        // causa (e a resolução: reativar ou mesclar) é diferente.
        if (!tipo.Ativo)
        {
            return new ResultadoDistribuicao.Excecao("tipo desativado", []);
        }

        var candidatosNaEscala = conferentes.Where(c => c.NaEscala).ToList();

        var avaliacoes = candidatosNaEscala
            .Select(c => new AvaliacaoCandidato(
                c,
                DecisaoEtapa: ResolvedorAlcada.Resolver(c, new AlvoAlcada.PorEtapa(protocolo.Etapa), regras),
                // .Value é seguro aqui: se TipoAtoId fosse nulo, já teríamos retornado acima.
                DecisaoTipo: ResolvedorAlcada.Resolver(c, new AlvoAlcada.PorTipoAto(protocolo.TipoAtoId!.Value), regras),
                DecisaoEquipe: ResolvedorAlcada.Resolver(c, new AlvoAlcada.PorEquipeDeEscrevente(equipeDoEscreventeId), regras)))
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
        return new ResultadoDistribuicao.Atribuido(escolhido.Conferente, escolhido, elegiveis);
    }
}
