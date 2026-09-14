namespace Dispatch.Domain.Tests;

public class UsuarioTests
{
    [Fact]
    public void RedefinirSenha_TrocaHashEBumpaCarimboDeSessoes()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash-antigo", Papel.Conferente);
        var agora = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);

        usuario.RedefinirSenha("hash-novo", agora);

        Assert.Equal("hash-novo", usuario.SenhaHash);
        Assert.Equal(agora, usuario.SessoesValidasApartirDe);
    }

    [Fact]
    public void RedefinirSenha_TruncaCarimboPraOSegundo()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash-antigo", Papel.Conferente);
        var agora = new DateTimeOffset(2026, 3, 5, 10, 0, 0, 823, TimeSpan.Zero);

        usuario.RedefinirSenha("hash-novo", agora);

        Assert.Equal(new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero), usuario.SessoesValidasApartirDe);
    }

    [Fact]
    public void SessoesValidasApartirDe_ComecaEmMinValue()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);

        Assert.Equal(DateTimeOffset.MinValue, usuario.SessoesValidasApartirDe);
    }

    [Fact]
    public void RegistrarTentativaLoginFalha_AntesDaQuintaNaoBloqueia()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var agora = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 4; i++)
        {
            usuario.RegistrarTentativaLoginFalha(agora);
        }

        Assert.False(usuario.EstaBloqueado(agora));
    }

    [Fact]
    public void RegistrarTentativaLoginFalha_NaQuintaBloqueiaPor15Minutos()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var agora = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);

        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarTentativaLoginFalha(agora);
        }

        Assert.True(usuario.EstaBloqueado(agora));
        Assert.False(usuario.EstaBloqueado(agora.AddMinutes(15).AddSeconds(1)));
    }

    [Fact]
    public void RegistrarLoginComSucesso_ZeraTentativasEDesbloqueia()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var agora = new DateTimeOffset(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 5; i++)
        {
            usuario.RegistrarTentativaLoginFalha(agora);
        }

        usuario.RegistrarLoginComSucesso();

        Assert.False(usuario.EstaBloqueado(agora));
        Assert.Equal(0, usuario.TentativasLoginFalhas);
    }
}
