using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class EditarPerfilConferenteTests
{
    [Fact]
    public async Task AtualizaNomeEEmail()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var usuarios = new FakeUsuarioRepository([usuario]);
        var casoDeUso = new EditarPerfilConferente(new FakeConferenteRepository([conferente]), usuarios, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(conferente.Id, "Fulano da Silva", "fulano.silva@cartorio.com");

        Assert.IsType<ResultadoEditarPerfilConferente.Sucesso>(resultado);
        var atualizado = await usuarios.ObterPorIdAsync(usuario.Id, CancellationToken.None);
        Assert.Equal("Fulano da Silva", atualizado!.Nome);
        Assert.Equal("fulano.silva@cartorio.com", atualizado.Email);
    }

    [Fact]
    public async Task ManterOMesmoEmail_NaoRejeitaPorConflitoConsigoMesmo()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new EditarPerfilConferente(
            new FakeConferenteRepository([conferente]), new FakeUsuarioRepository([usuario]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(conferente.Id, "Fulano", "fulano@cartorio.com");

        Assert.IsType<ResultadoEditarPerfilConferente.Sucesso>(resultado);
    }

    [Fact]
    public async Task EmailDeOutraPessoa_Rejeita()
    {
        var usuarioAlvo = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var outroUsuario = new Usuario(Guid.NewGuid(), "Ciclano", "ciclano@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuarioAlvo.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new EditarPerfilConferente(
            new FakeConferenteRepository([conferente]), new FakeUsuarioRepository([usuarioAlvo, outroUsuario]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(conferente.Id, "Fulano", "ciclano@cartorio.com");

        Assert.IsType<ResultadoEditarPerfilConferente.EmailJaCadastrado>(resultado);
    }

    [Fact]
    public async Task ConferenteInexistente_NaoEncontrado()
    {
        var casoDeUso = new EditarPerfilConferente(
            new FakeConferenteRepository([]), new FakeUsuarioRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), "Fulano", "fulano@cartorio.com");

        Assert.IsType<ResultadoEditarPerfilConferente.NaoEncontrado>(resultado);
    }
}
