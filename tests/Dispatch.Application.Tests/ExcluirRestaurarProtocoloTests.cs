using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ExcluirRestaurarProtocoloTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private static Protocolo NovoProtocoloAtribuido()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "999030", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, Agora);
        protocolo.AtribuirA(Guid.NewGuid(), Agora);
        return protocolo;
    }

    [Fact]
    public async Task Excluir_ProtocoloInexistente_DevolveFalse()
    {
        var casoDeUso = new ExcluirProtocolo(new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var achou = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.False(achou);
    }

    [Fact]
    public async Task Excluir_MarcaComoExcluidoEGuardaOStatusAnterior()
    {
        var protocolo = NovoProtocoloAtribuido();
        var casoDeUso = new ExcluirProtocolo(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var achou = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.True(achou);
        Assert.Equal(StatusProtocolo.Excluido, protocolo.Status);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.StatusAntesDeExcluir);
    }

    [Fact]
    public async Task Restaurar_ProtocoloQueNaoEstaExcluido_DevolveFalse()
    {
        var protocolo = NovoProtocoloAtribuido();
        var casoDeUso = new RestaurarProtocolo(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var achou = await casoDeUso.ExecutarAsync(protocolo.Id);

        Assert.False(achou);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
    }

    [Fact]
    public async Task Restaurar_DevolveOMesmoDonoEVencimento()
    {
        var protocolo = NovoProtocoloAtribuido();
        var donoOriginal = protocolo.DonoId;
        var vencimentoOriginal = protocolo.VencimentoEm;
        var excluir = new ExcluirProtocolo(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());
        await excluir.ExecutarAsync(protocolo.Id);
        var restaurar = new RestaurarProtocolo(new FakeProtocoloRepository([protocolo]), new FakeUnitOfWork());

        var achou = await restaurar.ExecutarAsync(protocolo.Id);

        Assert.True(achou);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Null(protocolo.StatusAntesDeExcluir);
        Assert.Equal(donoOriginal, protocolo.DonoId);
        Assert.Equal(vencimentoOriginal, protocolo.VencimentoEm);
    }

    [Fact]
    public async Task ProtocoloExcluido_NaoAparecePraDistribuicao()
    {
        var protocolo = NovoProtocoloAtribuido();
        protocolo.Excluir();
        var repositorio = new FakeProtocoloRepository([protocolo]);

        var visiveis = await repositorio.ObterParaDistribuicaoAsync(null, CancellationToken.None);

        Assert.Empty(visiveis);
    }

    [Fact]
    public async Task ProtocoloExcluido_NaoEntraNoRecalculoDeVencimentosAbertos()
    {
        var protocolo = NovoProtocoloAtribuido();
        protocolo.Excluir();
        var repositorio = new FakeProtocoloRepository([protocolo]);

        var abertos = await repositorio.ObterAbertosPorEscreventesAsync([protocolo.EscreventeId], CancellationToken.None);

        Assert.Empty(abertos);
    }
}
