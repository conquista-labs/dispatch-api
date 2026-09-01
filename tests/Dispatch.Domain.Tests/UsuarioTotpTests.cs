namespace Dispatch.Domain.Tests;

public class UsuarioTotpTests
{
    private static UsuarioTotp Novo() => new(Guid.NewGuid(), "segredo-cifrado", DateTimeOffset.UtcNow);

    [Fact]
    public void IniciarRegistro_LimpaConfirmacaoEContadorAnteriores()
    {
        var totp = Novo();
        totp.ConfirmarRegistro(contador: 42, DateTimeOffset.UtcNow);

        totp.IniciarRegistro("segredo-novo");

        Assert.Equal("segredo-novo", totp.SegredoCifrado);
        Assert.Null(totp.ConfirmadoEm);
        Assert.Null(totp.UltimoContadorAceito);
    }

    [Fact]
    public void RegistrarTentativaFalha_BloqueiaPor15MinutosNaQuintaTentativa()
    {
        var totp = Novo();
        var agora = DateTimeOffset.UtcNow;

        for (var i = 0; i < 4; i++)
        {
            totp.RegistrarTentativaFalha(agora);
        }
        Assert.Null(totp.BloqueadoAte);

        totp.RegistrarTentativaFalha(agora);

        Assert.Equal(5, totp.TentativasFalhas);
        Assert.Equal(agora.AddMinutes(15), totp.BloqueadoAte);
    }

    [Fact]
    public void RegistrarSucesso_ZeraTentativasEBloqueio()
    {
        var totp = Novo();
        var agora = DateTimeOffset.UtcNow;
        for (var i = 0; i < 5; i++)
        {
            totp.RegistrarTentativaFalha(agora);
        }

        totp.RegistrarSucesso(contador: 99);

        Assert.Equal(0, totp.TentativasFalhas);
        Assert.Null(totp.BloqueadoAte);
        Assert.Equal(99, totp.UltimoContadorAceito);
    }

    [Fact]
    public void ConsumirTokenRecuperacao_TornaOTokenInutilizavelDeNovo()
    {
        var totp = Novo();
        totp.EmitirTokenRecuperacao("hash-do-token", DateTimeOffset.UtcNow.AddMinutes(10));

        totp.ConsumirTokenRecuperacao();

        Assert.Null(totp.TokenRecuperacaoHash);
        Assert.Null(totp.TokenRecuperacaoExpiraEm);
    }
}
