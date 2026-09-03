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

        var caso = new CasoAlcada(protocolo.Etapa, tipo, equipeDoEscreventeId);
        var avaliacoes = candidatosNaEscala
            .Select(c => new AvaliacaoCandidato(c, ResolvedorAlcada.Resolver(c, caso, regras)))
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

        var escolhido = EscolherMenosCarregado(elegiveis, a => a.Conferente);
        return new ResultadoDistribuicao.Atribuido(escolhido.Conferente, escolhido, elegiveis);
    }

    // Desempate único do motor ("menor carga vence") — extraído pra ser reaproveitado por
    // AtribuirAoMenosCarregado (Application), que atribui na mão fora do caminho normal do
    // motor mas precisa do mesmo critério, sem reimplementar a regra.
    public static T EscolherMenosCarregado<T>(IReadOnlyCollection<T> candidatos, Func<T, Conferente> conferenteDe) =>
        candidatos.OrderBy(c => conferenteDe(c).CargaAtual).First();
}
