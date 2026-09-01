namespace Dispatch.Domain.Tests;

public class ResolvedorAlcadaTests
{
    private static readonly TipoAto Inventario = new(Guid.NewGuid(), "Inventário");
    private static readonly TipoAto Testamento = new(Guid.NewGuid(), "Testamento", grupo: GrupoTipoAto.Sucessoes);
    private static readonly TipoAto VendaECompra = new(Guid.NewGuid(), "Venda e Compra", grupo: GrupoTipoAto.Transmissoes);

    private static CasoAlcada Caso(Etapa etapa = Etapa.PreConferencia, TipoAto? tipo = null, Guid? equipeId = null) =>
        new(etapa, tipo ?? Inventario, equipeId);

    [Fact]
    public void RegraDeNivelNegaEAusenciaDeRegraPessoal_Nega()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var caso = Caso(Etapa.PreConferencia);
        var regra = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

        var decisao = ResolvedorAlcada.Resolver(conferente, caso, [regra]);

        Assert.Equal(ResultadoAlcada.Negado, decisao.Resultado);
        Assert.Equal(regra, decisao.RegraAplicada);
    }

    [Fact]
    public void RegraPessoalPermiteMesmoComRegraDeNivelNegandoOMesmoAlvo_Permite()
    {
        // Exemplo resolvido da seção 4 do documento de requisitos: Júnior com regra de
        // nível "não pode pré-conferência" e regra pessoal "pode pré e pós" -> pode fazer
        // pré-conferência — a camada "Exceção por pessoa" vence a "Base por nível".
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var caso = Caso(Etapa.PreConferencia);

        var regraDeNivel = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));
        var regraPessoal = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

        var decisao = ResolvedorAlcada.Resolver(conferente, caso, [regraDeNivel, regraPessoal]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
        Assert.Equal(regraPessoal, decisao.RegraAplicada);
    }

    [Fact]
    public void AusenciaDeRegraAplicavel_Permite()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var caso = Caso(Etapa.PosConferencia);

        var decisao = ResolvedorAlcada.Resolver(conferente, caso, []);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
        Assert.Null(decisao.RegraAplicada);
    }

    [Fact]
    public void DentroDaMesmaCamada_NegacaoVenceQuandoHaPermiteENegaParaOMesmoAlvo()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Senior, 8, naEscala: true, cargaAtual: 0);
        var caso = Caso(Etapa.PosConferencia);

        var permite = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Permite, new AlvoAlcada.PorEtapa(Etapa.PosConferencia));
        var nega = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PosConferencia));

        var decisao = ResolvedorAlcada.Resolver(conferente, caso, [permite, nega]);

        Assert.Equal(ResultadoAlcada.Negado, decisao.Resultado);
    }

    [Fact]
    public void RegraInativaNaoEhConsiderada()
    {
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var caso = Caso(Etapa.PreConferencia);

        var regraInativa = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia), ativa: false);

        var decisao = ResolvedorAlcada.Resolver(conferente, caso, [regraInativa]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
    }

    [Fact]
    public void ListaFechadaPorTipo_PermiteApenasOsTiposListadosPeloNivel()
    {
        // Confirmado ao vivo contra o simulador "Testar" do protótipo: nível com regras Permite
        // pra alguns tipos vira lista fechada — um tipo fora da lista é bloqueado mesmo sem
        // nenhuma regra de negação explícita pra ele.
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var regraPermiteInventario = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Permite, new AlvoAlcada.PorTipoAto(Inventario.Id));

        var decisaoListado = ResolvedorAlcada.Resolver(conferente, Caso(tipo: Inventario), [regraPermiteInventario]);
        var decisaoForaDaLista = ResolvedorAlcada.Resolver(conferente, Caso(tipo: Testamento), [regraPermiteInventario]);

        Assert.Equal(ResultadoAlcada.Permitido, decisaoListado.Resultado);
        Assert.Equal(ResultadoAlcada.Negado, decisaoForaDaLista.Resultado);
        Assert.Equal(regraPermiteInventario, decisaoForaDaLista.RegraAplicada);
    }

    [Fact]
    public void ListaFechadaPorEquipe_PermiteApenasAEquipeListadaPelaPessoa()
    {
        // RF-29a: pessoa com "pode conferir atos da equipe X" fica restrita só à equipe X —
        // "sem equipe" (nulo) também é um alvo bloqueável, não é tratado como "sem restrição".
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var equipeId = Guid.NewGuid();
        var regraPermiteEquipe = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, new AlvoAlcada.PorEquipeDeEscrevente(equipeId));

        var decisaoEquipeListada = ResolvedorAlcada.Resolver(conferente, Caso(equipeId: equipeId), [regraPermiteEquipe]);
        var decisaoSemEquipe = ResolvedorAlcada.Resolver(conferente, Caso(equipeId: null), [regraPermiteEquipe]);

        Assert.Equal(ResultadoAlcada.Permitido, decisaoEquipeListada.Resultado);
        Assert.Equal(ResultadoAlcada.Negado, decisaoSemEquipe.Resultado);
    }

    [Fact]
    public void AlcadaPlena_PermiteQualquerTipoMesmoComRegraDeNivelNegandoOutroTipo()
    {
        // RF-29b: alçada plena é checada dentro da própria camada pessoal, que vence a camada
        // de nível na cascata — uma negação de nível pra outro tipo não alcança quem tem
        // alçada plena pessoal.
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var alcadaPlena = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, new AlvoAlcada.PorTodosOsAtos());
        var negaDeNivelParaOutroTipo = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(Testamento.Id));

        var decisao = ResolvedorAlcada.Resolver(conferente, Caso(tipo: Testamento), [alcadaPlena, negaDeNivelParaOutroTipo]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
        Assert.Equal(alcadaPlena, decisao.RegraAplicada);
    }

    [Fact]
    public void AlcadaPlena_CedeANegacaoEspecificaDaMesmaCamada()
    {
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var alcadaPlena = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, new AlvoAlcada.PorTodosOsAtos());
        var negaEspecifica = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(Testamento.Id));

        var decisao = ResolvedorAlcada.Resolver(conferente, Caso(tipo: Testamento), [alcadaPlena, negaEspecifica]);

        Assert.Equal(ResultadoAlcada.Negado, decisao.Resultado);
        Assert.Equal(negaEspecifica, decisao.RegraAplicada);
    }

    [Fact]
    public void CamadaDeEquipeSobrescreveNegacaoDeEtapaDaCamadaDeNivel()
    {
        // "a de baixo vence a de cima": uma regra de equipe pra uma pessoa específica pode
        // liberar um caso que a base por nível bloquearia, mesmo numa dimensão (etapa) que a
        // regra de equipe nem menciona — porque a camada Equipe, quando opina, sobrescreve o
        // veredito inteiro da camada Nível, não só a dimensão que checou.
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var equipeBalcao = Guid.NewGuid();
        var negaPreDeNivel = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));
        var permiteEquipeBalcao = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite, new AlvoAlcada.PorEquipeDeEscrevente(equipeBalcao));

        var decisao = ResolvedorAlcada.Resolver(conferente, Caso(Etapa.PreConferencia, equipeId: equipeBalcao), [negaPreDeNivel, permiteEquipeBalcao]);

        Assert.Equal(ResultadoAlcada.Permitido, decisao.Resultado);
        Assert.Equal(permiteEquipeBalcao, decisao.RegraAplicada);
    }

    [Fact]
    public void Reserva_BloqueiaTodoMundoMenosOProprioSujeito()
    {
        var titular = Guid.NewGuid();
        var outraPessoa = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Senior, 8, naEscala: true, cargaAtual: 0);
        var reserva = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(titular), PermissaoRegra.Reserva, new AlvoAlcada.PorTipoAto(Testamento.Id));

        var decisao = ResolvedorAlcada.Resolver(outraPessoa, Caso(tipo: Testamento), [reserva]);

        Assert.Equal(ResultadoAlcada.Negado, decisao.Resultado);
        Assert.Equal(reserva, decisao.RegraAplicada);
        Assert.Equal(MotivoAlcada.Reservado, decisao.Motivo);
    }

    [Fact]
    public void Reserva_NaoConcedeAcessoSozinhaAoProprioSujeito()
    {
        // A reserva só bloqueia os outros — o titular ainda precisa de outra regra (ou do
        // padrão aberto) pra ter Permitido de verdade.
        var titularId = Guid.NewGuid();
        var titular = new Conferente(titularId, Guid.NewGuid(), Nivel.Senior, 8, naEscala: true, cargaAtual: 0);
        var reserva = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorPessoa(titularId), PermissaoRegra.Reserva, new AlvoAlcada.PorTipoAto(Testamento.Id));
        var negaDeNivel = new RegraAlcada(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(Testamento.Id));

        var semOutraRegra = ResolvedorAlcada.Resolver(titular, Caso(tipo: Testamento), [reserva]);
        var comNegacaoDeNivel = ResolvedorAlcada.Resolver(titular, Caso(tipo: Testamento), [reserva, negaDeNivel]);

        Assert.Equal(ResultadoAlcada.Permitido, semOutraRegra.Resultado); // padrão aberto, a reserva não impediu
        Assert.Equal(ResultadoAlcada.Negado, comNegacaoDeNivel.Resultado); // reserva não protege o titular de uma negação normal
    }

    [Fact]
    public void ListaFechadaPorGrupo_PermiteApenasOGrupoListado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var regraPermiteSucessoes = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Permite, new AlvoAlcada.PorGrupoTipoAto(GrupoTipoAto.Sucessoes));

        var decisaoDoGrupo = ResolvedorAlcada.Resolver(conferente, Caso(tipo: Testamento), [regraPermiteSucessoes]);
        var decisaoForaDoGrupo = ResolvedorAlcada.Resolver(conferente, Caso(tipo: VendaECompra), [regraPermiteSucessoes]);

        Assert.Equal(ResultadoAlcada.Permitido, decisaoDoGrupo.Resultado);
        Assert.Equal(ResultadoAlcada.Negado, decisaoForaDoGrupo.Resultado);
    }
}
