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

        var regraInativa = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Nega, alvo, Ativa: false);

        var decisao = ResolvedorAlcada.Resolver(conferente, alvo, [regraInativa]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
    }
}
