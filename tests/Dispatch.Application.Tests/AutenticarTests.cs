using Dispatch.Domain;

namespace Dispatch.Application.Tests;

internal sealed class FakeHashDeSenha : IHashDeSenha
{
    public string Hash(string senha) => $"hash:{senha}";
    public bool Verificar(string senhaHash, string senhaInformada) => senhaHash == Hash(senhaInformada);
}

internal sealed class FakeEmissorDeToken : IEmissorDeToken
{
    public string EmitirToken(Usuario usuario, IReadOnlyCollection<Papel> papeis) => $"token-para:{usuario.Email}";
}

public class AutenticarTests
{
    private static readonly FakeHashDeSenha HashDeSenha = new();
    private static readonly DateTimeOffset Agora = new(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);

    private static Autenticar NovoCasoDeUso(
        IReadOnlyCollection<Usuario> usuarios, out FakeEventoAutenticacaoRepository eventos, IReadOnlyCollection<Conferente>? conferentes = null)
    {
        eventos = new FakeEventoAutenticacaoRepository();
        return new Autenticar(
            new FakeUsuarioRepository(usuarios), new FakeConferenteRepository(conferentes ?? []), HashDeSenha, new FakeEmissorDeToken(),
            eventos, new FakeUnitOfWork(), new FakeRelogio(Agora));
    }

    [Fact]
    public async Task CredenciaisCorretas_EmiteToken()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = NovoCasoDeUso([usuario], out _);

        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-correta", origem: "127.0.0.1");

        var autenticado = Assert.IsType<ResultadoAutenticacao.Autenticado>(resultado);
        Assert.Equal("token-para:fulano@cartorio.com", autenticado.Token);
        Assert.Equal(usuario.Id, autenticado.UsuarioId);
        Assert.Equal("Fulano", autenticado.Nome);
        Assert.Equal("fulano@cartorio.com", autenticado.Email);
        Assert.Equal([Papel.Distribuidora], autenticado.Papeis);
    }

    // Pedido do dono: distribuidora que também confere — PapeisEfetivos soma Conferente quando
    // existe um Conferente vinculado ao Usuario, mesmo com Papel != Conferente.
    [Fact]
    public async Task DistribuidoraComConferenteVinculado_PapeisIncluiOsDois()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Maria", "maria@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var autenticar = NovoCasoDeUso([usuario], out _, [conferente]);

        var resultado = await autenticar.ExecutarAsync("maria@cartorio.com", "senha-correta", origem: null);

        var autenticado = Assert.IsType<ResultadoAutenticacao.Autenticado>(resultado);
        Assert.Equal([Papel.Distribuidora, Papel.Conferente], autenticado.Papeis);
    }

    // ADR-0039: o admin carrega também a claim Distribuidora (acesso de gestão), e o Administrador
    // vem primeiro — o front usa papeis[0] como papel principal.
    [Fact]
    public async Task Administrador_PapeisTrazAdministradorEDistribuidora()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Admin", "admin@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Administrador);
        var autenticar = NovoCasoDeUso([usuario], out _);

        var resultado = await autenticar.ExecutarAsync("admin@cartorio.com", "senha-correta", origem: null);

        var autenticado = Assert.IsType<ResultadoAutenticacao.Autenticado>(resultado);
        Assert.Equal([Papel.Administrador, Papel.Distribuidora], autenticado.Papeis);
    }

    // O caso de produção: a primeira admin também confere.
    [Fact]
    public async Task AdministradorComConferenteVinculado_PapeisTrazOsTres()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Admin", "admin@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Administrador);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var autenticar = NovoCasoDeUso([usuario], out _, [conferente]);

        var resultado = await autenticar.ExecutarAsync("admin@cartorio.com", "senha-correta", origem: null);

        var autenticado = Assert.IsType<ResultadoAutenticacao.Autenticado>(resultado);
        Assert.Equal([Papel.Administrador, Papel.Distribuidora, Papel.Conferente], autenticado.Papeis);
    }

    [Fact]
    public async Task DistribuidoraSemConferenteVinculado_PapeisSoTemDistribuidora()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = NovoCasoDeUso([usuario], out _);

        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-correta", origem: null);

        var autenticado = Assert.IsType<ResultadoAutenticacao.Autenticado>(resultado);
        Assert.Equal([Papel.Distribuidora], autenticado.Papeis);
    }

    [Fact]
    public async Task SenhaErrada_Rejeita()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = NovoCasoDeUso([usuario], out _);

        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-errada", origem: null);

        Assert.IsType<ResultadoAutenticacao.Rejeitado>(resultado);
    }

    [Fact]
    public async Task EmailNaoCadastrado_Rejeita()
    {
        var autenticar = NovoCasoDeUso([], out _);

        var resultado = await autenticar.ExecutarAsync("ninguem@cartorio.com", "qualquer-senha", origem: null);

        Assert.IsType<ResultadoAutenticacao.Rejeitado>(resultado);
    }

    [Fact]
    public async Task UsuarioInativo_Rejeita()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Conferente, ativo: false);
        var autenticar = NovoCasoDeUso([usuario], out _);

        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-correta", origem: null);

        Assert.IsType<ResultadoAutenticacao.Rejeitado>(resultado);
    }

    [Fact]
    public async Task SenhaErrada_RegistraTentativaEEventoComOrigem()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = NovoCasoDeUso([usuario], out var eventos);

        await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-errada", origem: "203.0.113.5");

        Assert.Equal(1, usuario.TentativasLoginFalhas);
        var evento = Assert.Single(eventos.Todos);
        Assert.Equal(TipoEventoAutenticacao.LoginFalhou, evento.Tipo);
        Assert.Equal("203.0.113.5", evento.Origem);
    }

    [Fact]
    public async Task QuintaSenhaErrada_BloqueiaLoginMesmoComSenhaCertaDepois()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = NovoCasoDeUso([usuario], out var eventos);

        for (var i = 0; i < 5; i++)
        {
            await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-errada", origem: null);
        }

        // Mesmo com a senha certa agora, a conta está bloqueada por 15 min (RF-01i, mesmo
        // mecanismo do código TOTP).
        var resultado = await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-correta", origem: null);

        Assert.IsType<ResultadoAutenticacao.Rejeitado>(resultado);
        Assert.Contains(eventos.Todos, e => e.Tipo == TipoEventoAutenticacao.LoginBloqueado);
    }

    [Fact]
    public async Task LoginComSucesso_ZeraTentativasAnteriores()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Fulano", "fulano@cartorio.com", HashDeSenha.Hash("senha-correta"), Papel.Distribuidora);
        var autenticar = NovoCasoDeUso([usuario], out _);

        await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-errada", origem: null);
        await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-errada", origem: null);
        await autenticar.ExecutarAsync("fulano@cartorio.com", "senha-correta", origem: null);

        Assert.Equal(0, usuario.TentativasLoginFalhas);
    }
}
