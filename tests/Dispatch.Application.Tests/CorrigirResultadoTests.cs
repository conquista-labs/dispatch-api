using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CorrigirResultadoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 29, 10, 0, 0, TimeSpan.Zero);

    private static Conferente NovoConferente() => new(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);

    private static Protocolo NovoProtocoloConcluido(Conferente dono, DateTimeOffset concluidoEm, bool aprovado = true)
    {
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(dono.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        if (aprovado)
        {
            protocolo.Aprovar(concluidoEm);
        }
        else
        {
            protocolo.Reprovar(concluidoEm);
        }

        return protocolo;
    }

    [Fact]
    public async Task DentroDaJanela_TrocaOResultado()
    {
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente, Agora.AddMinutes(-5));
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new CorrigirResultado(protocolos, new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.IsType<ResultadoCorrigirResultado.Sucesso>(resultado);
        Assert.Equal(StatusProtocolo.Reprovado, protocolo.Status);
        Assert.Equal(Agora, protocolo.CorrigidoEm);
    }

    [Fact]
    public async Task ForaDaJanela_Rejeita()
    {
        var conferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(conferente, Agora.AddMinutes(-16));
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new CorrigirResultado(protocolos, new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.IsType<ResultadoCorrigirResultado.ForaDaJanela>(resultado);
        Assert.Equal(StatusProtocolo.Aprovado, protocolo.Status);
    }

    [Fact]
    public async Task NaoEhODono_Rejeita()
    {
        var dono = NovoConferente();
        var outroConferente = NovoConferente();
        var protocolo = NovoProtocoloConcluido(dono, Agora.AddMinutes(-1));
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new CorrigirResultado(protocolos, new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, outroConferente);

        Assert.IsType<ResultadoCorrigirResultado.NaoEhSeu>(resultado);
    }

    [Fact]
    public async Task ProtocoloAindaEmConferencia_StatusInvalido()
    {
        var conferente = NovoConferente();
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        protocolo.IniciarConferencia(DateTimeOffset.UtcNow);
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new CorrigirResultado(protocolos, new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(protocolo.Id, conferente);

        Assert.IsType<ResultadoCorrigirResultado.StatusInvalido>(resultado);
    }

    [Fact]
    public async Task IdInexistente_NaoEncontrado()
    {
        var conferente = NovoConferente();
        var protocolos = new FakeProtocoloRepository([]);
        var casoDeUso = new CorrigirResultado(protocolos, new FakeRelogio(Agora), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), conferente);

        Assert.IsType<ResultadoCorrigirResultado.NaoEncontrado>(resultado);
    }
}
