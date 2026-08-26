using Dispatch.Domain;

namespace Dispatch.Application.Tests;

internal sealed class FakeHashDeSenha : IHashDeSenha
{
    public string Hash(string senha) => $"hash:{senha}";
    public bool Verificar(string senhaHash, string senhaInformada) => senhaHash == Hash(senhaInformada);
}

internal sealed class FakeEmissorDeToken : IEmissorDeToken
{
    public string EmitirToken(Usuario usuario) => $"token-para:{usuario.Email}";
}

public class AutenticarTests
{
    private static readonly FakeHashDeSenha HashDeSenha = new();

    [Fact]
    public async Task CredenciaisCorretas_EmiteToken()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = new Autenticar(new FakeUsuarioRepository([usuario]), HashDeSenha, new FakeEmissorDeToken());

        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-correta");

        var autenticado = Assert.IsType<ResultadoAutenticacao.Autenticado>(resultado);
        Assert.Equal("token-para:fulano@cartorio.com", autenticado.Token);
    }

    [Fact]
    public async Task SenhaErrada_Rejeita()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = new Autenticar(new FakeUsuarioRepository([usuario]), HashDeSenha, new FakeEmissorDeToken());

        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-errada");

        Assert.IsType<ResultadoAutenticacao.Rejeitado>(resultado);
    }

    [Fact]
    public async Task EmailNaoCadastrado_Rejeita()
    {
        var autenticar = new Autenticar(new FakeUsuarioRepository([]), HashDeSenha, new FakeEmissorDeToken());

        var resultado = await autenticar.ExecutarAsync("ninguem@cartorio.com", "qualquer-senha");

        Assert.IsType<ResultadoAutenticacao.Rejeitado>(resultado);
    }

    [Fact]
    public async Task UsuarioInativo_Rejeita()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Conferente, ativo: false);
        var autenticar = new Autenticar(new FakeUsuarioRepository([usuario]), HashDeSenha, new FakeEmissorDeToken());

        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-correta");

        Assert.IsType<ResultadoAutenticacao.Rejeitado>(resultado);
    }
}
