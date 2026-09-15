using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class VincularConferenteAUsuarioTests
{
    [Fact]
    public async Task UsuarioExistente_VinculaComSucesso()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Maria", "maria@cartorio.com", "hash", Papel.Distribuidora);
        var usuarios = new FakeUsuarioRepository([usuario]);
        var conferentes = new FakeConferenteRepository([]);
        var casoDeUso = new VincularConferenteAUsuario(usuarios, conferentes, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("maria@cartorio.com", Nivel.Pleno, 8, CancellationToken.None);

        var sucesso = Assert.IsType<ResultadoVincularConferente.Sucesso>(resultado);
        var conferente = await conferentes.ObterPorUsuarioIdAsync(usuario.Id, CancellationToken.None);
        Assert.NotNull(conferente);
        Assert.Equal(sucesso.ConferenteId, conferente.Id);
        Assert.Equal(usuario.Id, conferente.UsuarioId);
    }

    [Fact]
    public async Task UsuarioNaoEncontrado_Rejeita()
    {
        var casoDeUso = new VincularConferenteAUsuario(
            new FakeUsuarioRepository([]), new FakeConferenteRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("ninguem@cartorio.com", Nivel.Pleno, 8, CancellationToken.None);

        Assert.IsType<ResultadoVincularConferente.UsuarioNaoEncontrado>(resultado);
    }

    [Fact]
    public async Task UsuarioJaEhConferente_Rejeita()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var conferenteExistente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Junior, 6, naEscala: true, cargaAtual: 0);
        var casoDeUso = new VincularConferenteAUsuario(
            new FakeUsuarioRepository([usuario]), new FakeConferenteRepository([conferenteExistente]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("fulano@cartorio.com", Nivel.Pleno, 8, CancellationToken.None);

        Assert.IsType<ResultadoVincularConferente.JaEhConferente>(resultado);
    }
}
