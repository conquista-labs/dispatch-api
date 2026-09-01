using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ValidarCodigoRecuperacaoTests
{
    private static (Usuario usuario, UsuarioTotp totp) NovoUsuarioComTotpConfirmado(DateTimeOffset agora)
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var totp = new UsuarioTotp(usuario.Id, new FakeCifrador().Cifrar([1, 2, 3]), agora);
        totp.ConfirmarRegistro(1, agora);
        return (usuario, totp);
    }

    [Fact]
    public async Task CodigoValido_EmiteTokenComPrefixoDoUsuarioId()
    {
        var agora = DateTimeOffset.UtcNow;
        var (usuario, totp) = NovoUsuarioComTotpConfirmado(agora);
        var casoDeUso = new ValidarCodigoRecuperacao(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]), new FakeTotp(), new FakeCifrador(),
            new FakeHashDeSenha(), new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));

        var resultado = await casoDeUso.ExecutarAsync("fulano@cartorio.com", "123456");

        var tokenEmitido = Assert.IsType<ResultadoValidarCodigoRecuperacao.TokenEmitido>(resultado);
        Assert.StartsWith($"{usuario.Id:N}.", tokenEmitido.Token);
        Assert.NotNull(totp.TokenRecuperacaoHash);
    }

    [Fact]
    public async Task CodigoInvalido_IncrementaTentativas()
    {
        var agora = DateTimeOffset.UtcNow;
        var (usuario, totp) = NovoUsuarioComTotpConfirmado(agora);
        var casoDeUso = new ValidarCodigoRecuperacao(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]), new FakeTotp(), new FakeCifrador(),
            new FakeHashDeSenha(), new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));

        var resultado = await casoDeUso.ExecutarAsync("fulano@cartorio.com", "000000");

        Assert.IsType<ResultadoValidarCodigoRecuperacao.CodigoInvalido>(resultado);
        Assert.Equal(1, totp.TentativasFalhas);
    }

    [Fact]
    public async Task QuintaTentativaErrada_Bloqueia()
    {
        var agora = DateTimeOffset.UtcNow;
        var (usuario, totp) = NovoUsuarioComTotpConfirmado(agora);
        var casoDeUso = new ValidarCodigoRecuperacao(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]), new FakeTotp(), new FakeCifrador(),
            new FakeHashDeSenha(), new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));

        for (var i = 0; i < 4; i++)
        {
            await casoDeUso.ExecutarAsync("fulano@cartorio.com", "000000");
        }
        var resultado = await casoDeUso.ExecutarAsync("fulano@cartorio.com", "000000");

        var bloqueado = Assert.IsType<ResultadoValidarCodigoRecuperacao.Bloqueado>(resultado);
        Assert.Equal(agora.AddMinutes(15), bloqueado.BloqueadoAte);
    }

    [Fact]
    public async Task JaBloqueado_RejeitaSemChecarOCodigo()
    {
        var agora = DateTimeOffset.UtcNow;
        var (usuario, totp) = NovoUsuarioComTotpConfirmado(agora);
        for (var i = 0; i < 5; i++)
        {
            totp.RegistrarTentativaFalha(agora);
        }
        var casoDeUso = new ValidarCodigoRecuperacao(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]), new FakeTotp(), new FakeCifrador(),
            new FakeHashDeSenha(), new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));

        var resultado = await casoDeUso.ExecutarAsync("fulano@cartorio.com", "123456");

        Assert.IsType<ResultadoValidarCodigoRecuperacao.Bloqueado>(resultado);
    }

    [Fact]
    public async Task EmailInexistente_CodigoInvalidoGenerico()
    {
        var casoDeUso = new ValidarCodigoRecuperacao(
            new FakeUsuarioRepository([]), new FakeUsuarioTotpRepository([]), new FakeTotp(), new FakeCifrador(),
            new FakeHashDeSenha(), new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync("ninguem@cartorio.com", "123456");

        Assert.IsType<ResultadoValidarCodigoRecuperacao.CodigoInvalido>(resultado);
    }

    [Fact]
    public async Task TotpNaoConfirmado_CodigoInvalidoGenerico()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var totp = new UsuarioTotp(usuario.Id, new FakeCifrador().Cifrar([1, 2, 3]), DateTimeOffset.UtcNow);
        var casoDeUso = new ValidarCodigoRecuperacao(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]), new FakeTotp(), new FakeCifrador(),
            new FakeHashDeSenha(), new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync("fulano@cartorio.com", "123456");

        Assert.IsType<ResultadoValidarCodigoRecuperacao.CodigoInvalido>(resultado);
    }
}
