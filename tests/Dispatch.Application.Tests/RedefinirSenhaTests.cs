using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class RedefinirSenhaTests
{
    private const string SenhaForte = "cavalo azul correndo livre";

    private static (Usuario usuario, UsuarioTotp totp, string token) NovoUsuarioComTokenValido(FakeHashDeSenha hashDeSenha, DateTimeOffset agora)
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", "hash-antigo", Papel.Conferente);
        var totp = new UsuarioTotp(usuario.Id, "segredo-cifrado", agora);
        totp.ConfirmarRegistro(1, agora);
        const string tokenBruto = "token-bruto-de-teste";
        totp.EmitirTokenRecuperacao(hashDeSenha.Hash(tokenBruto), agora.AddMinutes(10));
        return (usuario, totp, $"{usuario.Id:N}.{tokenBruto}");
    }

    [Fact]
    public async Task TokenValidoESenhaForte_Redefine()
    {
        var agora = DateTimeOffset.UtcNow;
        var hashDeSenha = new FakeHashDeSenha();
        var (usuario, totp, token) = NovoUsuarioComTokenValido(hashDeSenha, agora);
        var casoDeUso = new RedefinirSenha(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]),
            new FakeConferenteRepository([]), new FakeProtocoloRepository([]),
            hashDeSenha, new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));

        var resultado = await casoDeUso.ExecutarAsync(token, SenhaForte);

        Assert.Equal(ResultadoRedefinirSenha.Sucesso, resultado);
        Assert.Equal(hashDeSenha.Hash(SenhaForte), usuario.SenhaHash);
        Assert.Equal(agora.AddTicks(-(agora.Ticks % TimeSpan.TicksPerSecond)), usuario.SessoesValidasApartirDe);
        Assert.Null(totp.TokenRecuperacaoHash);
    }

    [Fact]
    public async Task Redefine_DevolveProtocolosEmConferenciaParaOPool()
    {
        var agora = DateTimeOffset.UtcNow;
        var hashDeSenha = new FakeHashDeSenha();
        var (usuario, totp, token) = NovoUsuarioComTokenValido(hashDeSenha, agora);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 1);
        var protocolo = new Protocolo(Guid.NewGuid(), "123", Guid.NewGuid(), Guid.NewGuid(), Etapa.PreConferencia, agora);
        protocolo.AtribuirA(conferente.Id, agora);
        protocolo.IniciarConferencia(agora);
        var casoDeUso = new RedefinirSenha(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]),
            new FakeConferenteRepository([conferente]), new FakeProtocoloRepository([protocolo]),
            hashDeSenha, new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));

        await casoDeUso.ExecutarAsync(token, SenhaForte);

        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
        Assert.Null(protocolo.DonoId);
    }

    [Fact]
    public async Task TokenJaConsumido_NaoPodeSerReusado()
    {
        var agora = DateTimeOffset.UtcNow;
        var hashDeSenha = new FakeHashDeSenha();
        var (usuario, totp, token) = NovoUsuarioComTokenValido(hashDeSenha, agora);
        var casoDeUso = new RedefinirSenha(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]),
            new FakeConferenteRepository([]), new FakeProtocoloRepository([]),
            hashDeSenha, new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));
        await casoDeUso.ExecutarAsync(token, SenhaForte);

        var resultado = await casoDeUso.ExecutarAsync(token, "outra senha bem forte");

        Assert.Equal(ResultadoRedefinirSenha.TokenInvalido, resultado);
    }

    [Fact]
    public async Task TokenExpirado_Rejeita()
    {
        var agora = DateTimeOffset.UtcNow;
        var hashDeSenha = new FakeHashDeSenha();
        var (usuario, totp, token) = NovoUsuarioComTokenValido(hashDeSenha, agora);
        var casoDeUso = new RedefinirSenha(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]),
            new FakeConferenteRepository([]), new FakeProtocoloRepository([]),
            hashDeSenha, new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora.AddMinutes(11)));

        var resultado = await casoDeUso.ExecutarAsync(token, SenhaForte);

        Assert.Equal(ResultadoRedefinirSenha.TokenInvalido, resultado);
    }

    [Fact]
    public async Task TokenComFormatoInvalido_Rejeita()
    {
        var casoDeUso = new RedefinirSenha(
            new FakeUsuarioRepository([]), new FakeUsuarioTotpRepository([]),
            new FakeConferenteRepository([]), new FakeProtocoloRepository([]),
            new FakeHashDeSenha(), new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(DateTimeOffset.UtcNow));

        var resultado = await casoDeUso.ExecutarAsync("token-sem-ponto", SenhaForte);

        Assert.Equal(ResultadoRedefinirSenha.TokenInvalido, resultado);
    }

    [Fact]
    public async Task SenhaFraca_Rejeita()
    {
        var agora = DateTimeOffset.UtcNow;
        var hashDeSenha = new FakeHashDeSenha();
        var (usuario, totp, token) = NovoUsuarioComTokenValido(hashDeSenha, agora);
        var casoDeUso = new RedefinirSenha(
            new FakeUsuarioRepository([usuario]), new FakeUsuarioTotpRepository([totp]),
            new FakeConferenteRepository([]), new FakeProtocoloRepository([]),
            hashDeSenha, new FakeEventoAutenticacaoRepository(), new FakeUnitOfWork(), new FakeRelogio(agora));

        var resultado = await casoDeUso.ExecutarAsync(token, "curta");

        Assert.Equal(ResultadoRedefinirSenha.SenhaFraca, resultado);
        Assert.Equal("hash-antigo", usuario.SenhaHash);
    }
}
