using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class IniciarRecuperacaoSenhaTests
{
    [Fact]
    public async Task EmailExistente_RegistraEvento()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var eventos = new FakeEventoAutenticacaoRepository();
        var casoDeUso = new IniciarRecuperacaoSenha(
            new FakeUsuarioRepository([usuario]), eventos, new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        await casoDeUso.ExecutarAsync("fulano@cartorio.com");

        Assert.Single(eventos.Todos, e => e.Tipo == TipoEventoAutenticacao.RecuperacaoIniciada && e.UsuarioId == usuario.Id);
    }

    [Fact]
    public async Task EmailInexistente_NaoLancaENaoRegistraEvento()
    {
        var eventos = new FakeEventoAutenticacaoRepository();
        var casoDeUso = new IniciarRecuperacaoSenha(
            new FakeUsuarioRepository([]), eventos, new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        await casoDeUso.ExecutarAsync("ninguem@cartorio.com");

        Assert.Empty(eventos.Todos);
    }
}
