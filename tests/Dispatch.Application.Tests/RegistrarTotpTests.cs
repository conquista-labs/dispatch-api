using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class RegistrarTotpTests
{
    [Fact]
    public async Task UsuarioExistente_GravaRegistroPendenteEDevolveSegredoEUri()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var usuariosTotp = new FakeUsuarioTotpRepository([]);
        var casoDeUso = new RegistrarTotp(
            new FakeUsuarioRepository([usuario]),
            usuariosTotp,
            new FakeTotp(),
            new FakeCifrador(),
            new FakeEventoAutenticacaoRepository(),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(usuario.Id);

        Assert.NotNull(resultado);
        Assert.Contains(usuario.Email, resultado!.UriOtpAuth);
        var registro = await usuariosTotp.ObterPorUsuarioIdAsync(usuario.Id, default);
        Assert.NotNull(registro);
        Assert.Null(registro!.ConfirmadoEm);
    }

    [Fact]
    public async Task RegistroJaExistente_SobrescreveESoltaAConfirmacaoAnterior()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash", Papel.Conferente);
        var totpExistente = new UsuarioTotp(usuario.Id, "segredo-antigo-cifrado", DateTimeOffset.UtcNow);
        totpExistente.ConfirmarRegistro(1, DateTimeOffset.UtcNow);
        var usuariosTotp = new FakeUsuarioTotpRepository([totpExistente]);
        var casoDeUso = new RegistrarTotp(
            new FakeUsuarioRepository([usuario]),
            usuariosTotp,
            new FakeTotp(),
            new FakeCifrador(),
            new FakeEventoAutenticacaoRepository(),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        await casoDeUso.ExecutarAsync(usuario.Id);

        var registro = await usuariosTotp.ObterPorUsuarioIdAsync(usuario.Id, default);
        Assert.Null(registro!.ConfirmadoEm);
    }

    [Fact]
    public async Task UsuarioInexistente_RetornaNulo()
    {
        var casoDeUso = new RegistrarTotp(
            new FakeUsuarioRepository([]),
            new FakeUsuarioTotpRepository([]),
            new FakeTotp(),
            new FakeCifrador(),
            new FakeEventoAutenticacaoRepository(),
            new FakeUnitOfWork(),
            new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.Null(resultado);
    }
}
