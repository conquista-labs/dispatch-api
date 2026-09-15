using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterUsuarioAtualTests
{
    [Fact]
    public async Task UsuarioExistente_DevolveOsDados()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var casoDeUso = new ObterUsuarioAtual(new FakeUsuarioRepository([usuario]), new FakeConferenteRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(usuario.Id);

        Assert.NotNull(resultado);
        Assert.Equal("Fulano", resultado.Nome);
        Assert.Equal([Papel.Conferente], resultado.Papeis);
    }

    // Mesmo caso de PapeisEfetivos que AutenticarTests cobre pro login — aqui pro
    // GET /auth/me (revalidação de sessão, mesmo cálculo).
    [Fact]
    public async Task DistribuidoraComConferenteVinculado_PapeisIncluiOsDois()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Maria", "maria@cartorio.com", "hash", Papel.Distribuidora);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new ObterUsuarioAtual(new FakeUsuarioRepository([usuario]), new FakeConferenteRepository([conferente]));

        var resultado = await casoDeUso.ExecutarAsync(usuario.Id);

        Assert.NotNull(resultado);
        Assert.Equal([Papel.Distribuidora, Papel.Conferente], resultado.Papeis);
    }

    [Fact]
    public async Task UsuarioInexistente_DevolveNulo()
    {
        var casoDeUso = new ObterUsuarioAtual(new FakeUsuarioRepository([]), new FakeConferenteRepository([]));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.Null(resultado);
    }
}
