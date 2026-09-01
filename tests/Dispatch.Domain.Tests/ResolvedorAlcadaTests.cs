namespace Dispatch.Domain.Tests;

public class ResolvedorAlcadaTests
{
    [Fact]
    public void RegraDeNivelNegaEAusenciaDeRegraPessoal_Nega()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var alvo = new AlvoAlcada.PorEtapa(Etapa.PreConferencia);
        var regra = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, alvo);

        var decisao = ResolvedorAlcada.Resolver(conferente, alvo, [regra]);

        Assert.Equal(ResultadoAlcada.Negado, decisao.Resultado);
        Assert.Equal(regra, decisao.RegraAplicada);
    }

    [Fact]
    public void RegraPessoalPermiteMesmoComRegraDeNivelNegandoOMesmoAlvo_Permite()
    {
        // Exemplo resolvido da seção 4 do documento de requisitos: Júnior com regra de
        // nível "não pode pré-conferência" e regra pessoal "pode pré e pós" -> pode fazer
        // pré-conferência, porque a regra pessoal cobre o alvo e substitui a de nível.
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var alvo = new AlvoAlcada.PorEtapa(Etapa.PreConferencia);

        var regraDeNivel = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, alvo);
        var regraPessoal = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, alvo);

        var decisao = ResolvedorAlcada.Resolver(conferente, alvo, [regraDeNivel, regraPessoal]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
        Assert.Equal(regraPessoal, decisao.RegraAplicada);
    }

    [Fact]
    public void AusenciaDeRegraAplicavel_Permite()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var alvo = new AlvoAlcada.PorEtapa(Etapa.PosConferencia);

        var decisao = ResolvedorAlcada.Resolver(conferente, alvo, []);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
        Assert.Null(decisao.RegraAplicada);
    }

    [Fact]
    public void DentroDoMesmoEscopo_NegacaoVenceQuandoHaPermiteENegaParaOMesmoAlvo()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Senior, 8, naEscala: true, cargaAtual: 0);
        var alvo = new AlvoAlcada.PorEtapa(Etapa.PosConferencia);

        var permite = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Permite, alvo);
        var nega = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Nega, alvo);

        var decisao = ResolvedorAlcada.Resolver(conferente, alvo, [permite, nega]);

        Assert.Equal(ResultadoAlcada.Negado, decisao.Resultado);
    }

    [Fact]
    public void RegraInativaNaoEhConsiderada()
    {
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var alvo = new AlvoAlcada.PorEtapa(Etapa.PreConferencia);

        var regraInativa = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Nega, alvo, ativa: false);

        var decisao = ResolvedorAlcada.Resolver(conferente, alvo, [regraInativa]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
    }

    [Fact]
    public void ListaFechadaPorTipo_PermiteApenasOsTiposListadosPeloNivel()
    {
        // Confirmado ao vivo contra o simulador "Testar" do protótipo v2: nível com regras
        // Permite pra alguns tipos vira lista fechada — um tipo fora da lista é bloqueado
        // mesmo sem nenhuma regra de negação explícita pra ele.
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var tipoListado = Guid.NewGuid();
        var tipoForaDaLista = Guid.NewGuid();
        var regraPermiteTipoListado = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Permite, new AlvoAlcada.PorTipoAto(tipoListado));

        var decisaoListado = ResolvedorAlcada.Resolver(conferente, new AlvoAlcada.PorTipoAto(tipoListado), [regraPermiteTipoListado]);
        var decisaoForaDaLista = ResolvedorAlcada.Resolver(conferente, new AlvoAlcada.PorTipoAto(tipoForaDaLista), [regraPermiteTipoListado]);

        Assert.Equal(ResultadoAlcada.Permitido, decisaoListado.Resultado);
        Assert.Equal(ResultadoAlcada.Negado, decisaoForaDaLista.Resultado);
        Assert.Equal(regraPermiteTipoListado, decisaoForaDaLista.RegraAplicada);
    }

    [Fact]
    public void ListaFechadaPorEquipe_PermiteApenasAEquipeListadaPelaPessoa()
    {
        // Mesmo padrão confirmado ao vivo, agora na dimensão equipe (RF-29a): pessoa com
        // "pode conferir atos da equipe X" fica restrita só à equipe X — "sem equipe" (nulo)
        // também é um alvo bloqueável, não é tratado como "sem restrição".
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var equipeId = Guid.NewGuid();
        var regraPermiteEquipe = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite,
            new AlvoAlcada.PorEquipeDeEscrevente(equipeId));

        var decisaoEquipeListada = ResolvedorAlcada.Resolver(conferente, new AlvoAlcada.PorEquipeDeEscrevente(equipeId), [regraPermiteEquipe]);
        var decisaoSemEquipe = ResolvedorAlcada.Resolver(conferente, new AlvoAlcada.PorEquipeDeEscrevente(null), [regraPermiteEquipe]);

        Assert.Equal(ResultadoAlcada.Permitido, decisaoEquipeListada.Resultado);
        Assert.Equal(ResultadoAlcada.Negado, decisaoSemEquipe.Resultado);
    }

    [Fact]
    public void AlcadaPlena_PermiteQualquerTipoMesmoComRegraDeNivelNegandoOutroTipo()
    {
        // RF-29b: alçada plena é checada dentro do próprio escopo pessoal antes de cair pro
        // nível — senão uma negação de nível pra um tipo qualquer bloquearia até quem tem
        // alçada plena pessoal, o que contrariaria "continua sujeita às restrições explícitas
        // de negação" (a negação relevante é a do MESMO escopo, não de outro).
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var tipoQualquer = Guid.NewGuid();
        var alcadaPlena = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, new AlvoAlcada.PorTodosOsAtos());
        var negaDeNivelParaOutroTipo = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipoQualquer));

        var decisao = ResolvedorAlcada.Resolver(conferente, new AlvoAlcada.PorTipoAto(tipoQualquer), [alcadaPlena, negaDeNivelParaOutroTipo]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
        Assert.Equal(alcadaPlena, decisao.RegraAplicada);
    }

    [Fact]
    public void AlcadaPlena_CedeANegacaoEspecificaDoMesmoEscopo()
    {
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var tipoNegado = Guid.NewGuid();
        var alcadaPlena = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, new AlvoAlcada.PorTodosOsAtos());
        var negaEspecifica = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipoNegado));

        var decisao = ResolvedorAlcada.Resolver(conferente, new AlvoAlcada.PorTipoAto(tipoNegado), [alcadaPlena, negaEspecifica]);

        Assert.Equal(ResultadoAlcada.Negado, decisao.Resultado);
        Assert.Equal(negaEspecifica, decisao.RegraAplicada);
    }
}
