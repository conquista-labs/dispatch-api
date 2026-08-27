using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class RedistribuirPoolTests
{
    private static readonly TipoAto Inventario = new(Guid.NewGuid(), "Inventário");

    private static Protocolo NovoProtocoloEmExcecao(string motivo)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.MarcarExcecao(motivo);
        return protocolo;
    }

    [Fact]
    public async Task ExcecaoPorFaltaDeAlcada_VoltaAEstarElegivelQuandoConferenteApareceu()
    {
        var protocolo = NovoProtocoloEmExcecao("ninguém com alçada");
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new RedistribuirPool(
            protocolos,
            new FakeConferenteRepository([conferente]),
            new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([Inventario]),
            new FakeUnitOfWork());

        var alterados = await casoDeUso.ExecutarAsync();

        Assert.Equal(1, alterados);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
    }

    [Fact]
    public async Task SemMudancaDeElegibilidade_NaoContaComoAlterado()
    {
        var protocolo = NovoProtocoloEmExcecao("ninguém com alçada");
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new RedistribuirPool(
            protocolos,
            new FakeConferenteRepository([]),
            new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([Inventario]),
            new FakeUnitOfWork());

        var alterados = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, alterados);
        Assert.Equal(StatusProtocolo.Excecao, protocolo.Status);
    }

    [Fact]
    public async Task ProtocoloAtribuido_NaoEntraNaRedistribuicao()
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Inventario.Id, Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid());
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new RedistribuirPool(
            protocolos,
            new FakeConferenteRepository([]),
            new FakeRegraAlcadaRepository([]),
            new FakeTipoAtoRepository([Inventario]),
            new FakeUnitOfWork());

        var alterados = await casoDeUso.ExecutarAsync();

        Assert.Equal(0, alterados);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
    }
}
