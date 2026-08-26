using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CadastrarConferenteTests
{
    [Fact]
    public async Task EmailNovo_CriaUsuarioEConferente()
    {
        var usuarios = new FakeUsuarioRepository([]);
        var conferentes = new FakeConferenteRepository([]);
        var casoDeUso = new CadastrarConferente(usuarios, conferentes, new FakeHashDeSenha(), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("Fulano", "fulano@cartorio.com", "senha-123", Nivel.Junior, jornadaHoras: 8);

        var sucesso = Assert.IsType<ResultadoCadastroConferente.Sucesso>(resultado);
        var usuario = await usuarios.ObterPorEmailAsync("fulano@cartorio.com", CancellationToken.None);
        Assert.NotNull(usuario);
        Assert.Equal(Papel.Conferente, usuario.Papel);
        var conferente = await conferentes.ObterPorIdAsync(sucesso.ConferenteId, CancellationToken.None);
        Assert.NotNull(conferente);
        Assert.Equal(usuario.Id, conferente.UsuarioId);
    }

    [Fact]
    public async Task EmailJaCadastrado_Rejeita()
    {
        var usuarioExistente = new Usuario(Guid.NewGuid(), "Outro", "fulano@cartorio.com", "hash", Papel.Distribuidora);
        var casoDeUso = new CadastrarConferente(
            new FakeUsuarioRepository([usuarioExistente]),
            new FakeConferenteRepository([]),
            new FakeHashDeSenha(),
            new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("Fulano", "fulano@cartorio.com", "senha-123", Nivel.Junior, jornadaHoras: 8);

        Assert.IsType<ResultadoCadastroConferente.EmailJaCadastrado>(resultado);
    }
}
