namespace Dispatch.Domain.Tests;

public class MotorDistribuicaoTests
{
    private static readonly TipoAto Inventario = new(Guid.NewGuid(), "Inventário");

    [Fact]
    public void TipoDeAtoDesconhecido_RetornaExcecao()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [], [], [Inventario]);

        var excecao = Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
        Assert.Equal("tipo desconhecido", excecao.Motivo);
    }

    [Fact]
    public void TipoDeAtoDesativado_RetornaExcecaoComMotivoDistinto()
    {
        // RF-34d: desativar um tipo não apaga histórico, mas o próximo protocolo desse tipo
        // vai para exceção com um motivo diferente de "tipo desconhecido" — a causa (e a
        // resolução: reativar ou mesclar) é outra.
        var inventarioDesativado = new TipoAto(Guid.NewGuid(), "Inventário", ativo: false);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", inventarioDesativado.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [], [], [inventarioDesativado]);

        var excecao = Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
        Assert.Equal("tipo desativado", excecao.Motivo);
    }

    [Fact]
    public void NenhumConferenteComAlcada_RetornaExcecaoComMotivoPorPessoa()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
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
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var ausente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: false, cargaAtual: 0);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [ausente], [], [Inventario]);

        var excecao = Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
        Assert.Empty(excecao.Avaliacoes);
    }

    [Fact]
    public void ProtocoloNaoUrgenteComCandidatoElegivel_VaiParaOPool()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [conferente], [], [Inventario]);

        var pool = Assert.IsType<ResultadoDistribuicao.EnviadoParaPool>(resultado);
        Assert.Single(pool.Elegiveis);
    }

    [Fact]
    public void ProtocoloUrgente_AtribuiAoCandidatoComMenorCargaAtual()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow, Prioridade.Alta);
        var maisCarregado = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 5);
        var menosCarregado = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 1);

        var resultado = MotorDistribuicao.Distribuir(protocolo, [maisCarregado, menosCarregado], [], [Inventario]);

        var atribuido = Assert.IsType<ResultadoDistribuicao.Atribuido>(resultado);
        Assert.Equal(menosCarregado.Id, atribuido.Conferente.Id);
    }

    [Fact]
    public void RegraDeEquipeBloqueiaCandidatoQuePassariaEmTipoEEtapa()
    {
        // RF-29a: mesmo com tipo e etapa liberados, uma regra de equipe fora da lista fechada
        // barra o candidato — a alçada precisa das três dimensões permitidas ao mesmo tempo.
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var equipePermitida = Guid.NewGuid();
        var equipeDoEscrevente = Guid.NewGuid();
        var regraDeEquipe = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite,
            new AlvoAlcada.PorEquipeDeEscrevente(equipePermitida));

        var resultado = MotorDistribuicao.Distribuir(protocolo, [conferente], [regraDeEquipe], [Inventario], equipeDoEscrevente);

        var excecao = Assert.IsType<ResultadoDistribuicao.Excecao>(resultado);
        Assert.Equal("ninguém com alçada", excecao.Motivo);
    }

    [Fact]
    public void RegraDeEquipeLiberaCandidatoQuandoEquipeBate()
    {
        var conferenteId = Guid.NewGuid();
        var conferente = new Conferente(conferenteId, Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var equipeDoEscrevente = Guid.NewGuid();
        var regraDeEquipe = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorPessoa(conferenteId), PermissaoRegra.Permite,
            new AlvoAlcada.PorEquipeDeEscrevente(equipeDoEscrevente));

        var resultado = MotorDistribuicao.Distribuir(protocolo, [conferente], [regraDeEquipe], [Inventario], equipeDoEscrevente);

        var pool = Assert.IsType<ResultadoDistribuicao.EnviadoParaPool>(resultado);
        Assert.Single(pool.Elegiveis);
    }
}
