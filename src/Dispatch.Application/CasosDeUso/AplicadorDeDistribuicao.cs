using Dispatch.Domain;

namespace Dispatch.Application;

// Sequência compartilhada entre DistribuirProtocolo (avulso) e ImportarLote (em lote):
// resolve prazo, roda o motor, aplica o resultado no protocolo. Só orquestração — nenhuma
// regra nova, tudo já existe em Dispatch.Domain.
internal static class AplicadorDeDistribuicao
{
    public static ResultadoDistribuicao Executar(
        Protocolo protocolo,
        Escrevente escrevente,
        IReadOnlyCollection<Equipe> equipes,
        IReadOnlyCollection<Conferente> conferentesNaEscala,
        IReadOnlyCollection<RegraAlcada> regras,
        IReadOnlyCollection<TipoAto> catalogoTipos,
        DateTimeOffset agora,
        out ResolucaoPrazo resolucaoPrazo)
    {
        resolucaoPrazo = ResolvedorDePrazo.Resolver(escrevente, protocolo.Etapa, equipes);
        protocolo.DefinirPrazo(resolucaoPrazo.Prazo, protocolo.AndamentoEm);

        var resultado = MotorDistribuicao.Distribuir(protocolo, conferentesNaEscala, regras, catalogoTipos);

        switch (resultado)
        {
            case ResultadoDistribuicao.Atribuido atribuido:
                protocolo.AtribuirA(atribuido.Conferente.Id, agora, RegraAplicadaDe(atribuido.Avaliacao));
                break;
            case ResultadoDistribuicao.EnviadoParaPool:
                protocolo.EnviarParaPool();
                break;
            case ResultadoDistribuicao.Excecao excecao:
                protocolo.MarcarExcecao(excecao.Motivo);
                break;
        }

        return resultado;
    }

    // RNF-02: a regra que decidiu — prioriza a de tipo (mais específica, ver comentário em
    // Protocolo.RegraAplicadaId) sobre a de etapa; nulo quando os dois vieram do padrão aberto.
    private static Guid? RegraAplicadaDe(AvaliacaoCandidato avaliacao) =>
        avaliacao.DecisaoTipo.RegraAplicada?.Id ?? avaliacao.DecisaoEtapa.RegraAplicada?.Id;
}
