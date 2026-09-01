using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ConfirmarRegistroTotpTests
{
    [Fact]
    public async Task CodigoValido_ConfirmaERegistraEvento()
    {
        var usuarioId = Guid.NewGuid();
        var totp = new UsuarioTotp(usuarioId, new FakeCifrador().Cifrar([1, 2, 3]), DateTimeOffset.UtcNow);
        var eventos = new FakeEventoAutenticacaoRepository();
        var casoDeUso = new ConfirmarRegistroTotp(
            new FakeUsuarioTotpRepository([totp]),
            new FakeTotp(),
            new FakeCifrador(),
            eventos,
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(usuarioId, "123456");

        Assert.Equal(ResultadoConfirmarTotp.Sucesso, resultado);
        Assert.NotNull(totp.ConfirmadoEm);
        Assert.Single(eventos.Todos, e => e.Tipo == TipoEventoAutenticacao.RegistroTotpConfirmado);
    }

    [Fact]
    public async Task CodigoInvalido_NaoConfirma()
    {
        var usuarioId = Guid.NewGuid();
        var totp = new UsuarioTotp(usuarioId, new FakeCifrador().Cifrar([1, 2, 3]), DateTimeOffset.UtcNow);
        var casoDeUso = new ConfirmarRegistroTotp(
            new FakeUsuarioTotpRepository([totp]),
            new FakeTotp(),
            new FakeCifrador(),
            new FakeEventoAutenticacaoRepository(),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(usuarioId, "000000");

        Assert.Equal(ResultadoConfirmarTotp.CodigoInvalido, resultado);
        Assert.Null(totp.ConfirmadoEm);
    }

    [Fact]
    public async Task SemRegistroPendente_Retorna()
    {
        var casoDeUso = new ConfirmarRegistroTotp(
            new FakeUsuarioTotpRepository([]),
            new FakeTotp(),
            new FakeCifrador(),
            new FakeEventoAutenticacaoRepository(),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), "123456");

        Assert.Equal(ResultadoConfirmarTotp.SemRegistroPendente, resultado);
    }
}
