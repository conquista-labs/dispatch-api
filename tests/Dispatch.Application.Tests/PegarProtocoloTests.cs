using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class PegarProtocoloTests
{
    [Fact]
    public async Task ProtocoloNoPoolDentroDaAlcada_Atribui()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var casoDeUso = new PegarProtocolo(
            new FakeProtocoloRepository([protocolo]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]), new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoPegarProtocolo.Sucesso, resultado);
        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(conferente.Id, protocolo.DonoId);
    }

    [Fact]
    public async Task ProtocoloInexistente_RetornaNaoEncontrado()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new PegarProtocolo(
            new FakeProtocoloRepository([]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]), new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente);

        Assert.Equal(ResultadoPegarProtocolo.NaoEncontrado, resultado);
    }

    [Fact]
    public async Task ProtocoloForaDoPool_RetornaNaoEstaNoPool()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(Guid.NewGuid(), DateTimeOffset.UtcNow);
        var casoDeUso = new PegarProtocolo(
            new FakeProtocoloRepository([protocolo]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([]), new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoPegarProtocolo.NaoEstaNoPool, resultado);
    }

    [Fact]
    public async Task SemAlcadaPraEtapa_RetornaSemAlcada()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        var regra = new RegraAlcada(
            Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));
        var casoDeUso = new PegarProtocolo(
            new FakeProtocoloRepository([protocolo]), new FakeEscreventeRepository([]), new FakeRegraAlcadaRepository([regra]), new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.Equal(ResultadoPegarProtocolo.SemAlcada, resultado);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
    }
}
