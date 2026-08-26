namespace Dispatch.Domain.Tests;

public class MotorDistribuicaoTests
{
    private static readonly TipoAto Inventario = new(Guid.NewGuid(), "Inventário");

    [Fact]
    public void TipoDeAtoDesconhecido_RetornaExcecao()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Etapa.PreConferencia);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [], [], [Inventario]);

        var excecao = Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
        Assert.Equal("tipo desconhecido", excecao.Motivo);
    }

    [Fact]
    public void NenhumConferenteComAlcada_RetornaExcecaoComMotivoPorPessoa()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Etapa.PreConferencia);
        var conferente = new Conferente(Guid.NewGuid(), Nivel.Junior, naEscala: true, cargaAtual: 0);
        var regraNegaTipo = new RegraAlcada(
            Guid.NewGuid(),
            new SujeitoAlcada.PorNivel(Nivel.Junior),
            PermissaoRegra.Nega,
            new AlvoAlcada.PorTipoAto(Inventario.Id));

        var resultado = MotorDistribuicao.Distribuir(protocolo, [conferente], [regraNegaTipo], [Inventario]);

        var excecao = Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
        Assert.Equal("ninguém com alçada", excecao.Motivo);
        var avaliacao = Assert.Single(excecao.Avaliacoes);
        Assert.False(avaliacao.Elegivel);
        Assert.Equal(regraNegaTipo, avaliacao.DecisaoTipo.RegraAplicada);
    }

    [Fact]
    public void ConferenteForaDaEscala_NaoEhCandidato()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Etapa.PreConferencia);
        var ausente = new Conferente(Guid.NewGuid(), Nivel.Pleno, naEscala: false, cargaAtual: 0);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [ausente], [], [Inventario]);

        var excecao = Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
        Assert.Empty(excecao.Avaliacoes);
    }

    [Fact]
    public void ProtocoloNaoUrgenteComCandidatoElegivel_VaiParaOPool()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Etapa.PreConferencia);
        var conferente = new Conferente(Guid.NewGuid(), Nivel.Pleno, naEscala: true, cargaAtual: 0);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [conferente], [], [Inventario]);

        var pool = Assert.IsType<ResultadoDistribuicao.EnviadoParaPool>(resultado);
        Assert.Single(pool.Elegiveis);
    }

    [Fact]
    public void ProtocoloUrgente_AtribuiAoCandidatoComMenorCargaAtual()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Etapa.PreConferencia, Prioridade.Alta);
        var maisCarregado = new Conferente(Guid.NewGuid(), Nivel.Pleno, naEscala: true, cargaAtual: 5);
        var menosCarregado = new Conferente(Guid.NewGuid(), Nivel.Pleno, naEscala: true, cargaAtual: 1);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [maisCarregado, menosCarregado], [], [Inventario]);

        var atribuido = Assert.IsType<ResultadoDistribuicao.Atribuido>(resultado);
        Assert.Equal(menosCarregado.Id, atribuido.Conferente.Id);
    }
}
