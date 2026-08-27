using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class MarcarPresencaTests
{
    [Fact]
    public async Task ConferenteExistente_AtualizaNaEscala()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new MarcarPresenca(new FakeConferenteRepository([conferente]), new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(conferente.Id, presente: false);

        Assert.True(resultado);
        Assert.False(conferente.NaEscala);
    }

    [Fact]
    public async Task ConferenteInexistente_RetornaFalse()
    {
        var casoDeUso = new MarcarPresenca(new FakeConferenteRepository([]), new FakeProtocoloRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), presente: false);

        Assert.False(resultado);
    }

    [Fact]
    public async Task MarcarAusente_DevolveProtocolosAtribuidosParaOPool()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 1);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id);
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new MarcarPresenca(new FakeConferenteRepository([conferente]), protocolos, new FakeUnitOfWork());

        await casoDeUso.ExecutarAsync(conferente.Id, presente: false);

        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
        Assert.Null(protocolo.DonoId);
    }

    [Fact]
    public async Task MarcarPresente_NaoMexeNosProtocolosAtribuidos()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: false, cargaAtual: 1);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id);
        var protocolos = new FakeProtocoloRepository([protocolo]);
        var casoDeUso = new MarcarPresenca(new FakeConferenteRepository([conferente]), protocolos, new FakeUnitOfWork());

        await casoDeUso.ExecutarAsync(conferente.Id, presente: true);

        Assert.Equal(StatusProtocolo.Atribuido, protocolo.Status);
        Assert.Equal(conferente.Id, protocolo.DonoId);
    }
}
