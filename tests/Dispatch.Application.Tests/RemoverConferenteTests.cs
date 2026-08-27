using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class RemoverConferenteTests
{
    [Fact]
    public async Task ConferenteExistente_DesativaUsuarioESaiDaEscala()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new RemoverConferente(
            new FakeConferenteRepository([conferente]),
            new FakeUsuarioRepository([usuario]),
            new FakeProtocoloRepository([]),
            new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(conferente.Id);

        Assert.True(resultado);
        Assert.False(usuario.Ativo);
        Assert.False(conferente.NaEscala);
    }

    [Fact]
    public async Task ConferenteInexistente_RetornaFalse()
    {
        var casoDeUso = new RemoverConferente(
            new FakeConferenteRepository([]),
            new FakeUsuarioRepository([]),
            new FakeProtocoloRepository([]),
            new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.False(resultado);
    }

    [Fact]
    public async Task Remover_DevolveProtocolosAtribuidosParaOPool()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 1);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Etapa.PreConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id);
        var casoDeUso = new RemoverConferente(
            new FakeConferenteRepository([conferente]),
            new FakeUsuarioRepository([usuario]),
            new FakeProtocoloRepository([protocolo]),
            new FakeUnitOfWork());

        await casoDeUso.ExecutarAsync(conferente.Id);

        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
        Assert.Null(protocolo.DonoId);
    }
}
