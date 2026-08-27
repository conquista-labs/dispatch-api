using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterUsuarioAtualTests
{
    [Fact]
    public async Task UsuarioExistente_DevolveOsDados()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var casoDeUso = new ObterUsuarioAtual(new FakeUsuarioRepository([usuario]));

        var resultado = await casoDeUso.ExecutarAsync(usuario.Id);

        Assert.NotNull(resultado);
        Assert.Equal("Fulano", resultado.Nome);
    }

    [Fact]
    public async Task UsuarioInexistente_DevolveNulo()
    {
        var casoDeUso = new ObterUsuarioAtual(new FakeUsuarioRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.Null(resultado);
    }
}
