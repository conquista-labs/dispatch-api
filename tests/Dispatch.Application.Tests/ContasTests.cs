using Dispatch.Domain;

namespace Dispatch.Application.Tests;

// Perfil Administrador (ADR-0039) — Contas, RF-44 a RF-47.
public class CriarContaTests
{
    private static CriarConta NovoCasoDeUso(FakeUsuarioRepository usuarios) => new(usuarios, new FakeHashDeSenha(), new FakeUnitOfWork());

    [Theory]
    [InlineData(Papel.Distribuidora)]
    [InlineData(Papel.Administrador)]
    public async Task PapelDeGestao_CriaComTrocaDeSenhaObrigatoria(Papel papel)
    {
        var usuarios = new FakeUsuarioRepository([]);

        var resultado = await NovoCasoDeUso(usuarios).ExecutarAsync(" Vivi ", " vivi@cartorio.com ", "abcd-efgh-12", papel);

        var sucesso = Assert.IsType<ResultadoCriarConta.Sucesso>(resultado);
        var criado = await usuarios.ObterPorIdAsync(sucesso.UsuarioId, CancellationToken.None);
        Assert.Equal("Vivi", criado!.Nome);
        Assert.Equal("vivi@cartorio.com", criado.Email);
        Assert.Equal(papel, criado.Papel);
        Assert.True(criado.TrocarSenhaNoProximoAcesso);
    }

    [Fact]
    public async Task PapelConferente_Rejeita()
    {
        var resultado = await NovoCasoDeUso(new FakeUsuarioRepository([])).ExecutarAsync("A", "a@b.com", "abcdefgh", Papel.Conferente);
        Assert.IsType<ResultadoCriarConta.PapelInvalido>(resultado);
    }

    [Theory]
    [InlineData("", "a@cartorio.com")]
    [InlineData("Fulano", "sem-arroba")]
    [InlineData("Fulano", "a@semponto")]
    public async Task DadosInvalidos_Rejeita(string nome, string email)
    {
        var resultado = await NovoCasoDeUso(new FakeUsuarioRepository([])).ExecutarAsync(nome, email, "abcdefgh", Papel.Distribuidora);
        Assert.IsType<ResultadoCriarConta.DadosInvalidos>(resultado);
    }

    [Fact]
    public async Task SenhaInicialComMenosDe8_Rejeita()
    {
        var resultado = await NovoCasoDeUso(new FakeUsuarioRepository([])).ExecutarAsync("A", "a@b.com", "1234567", Papel.Distribuidora);
        Assert.IsType<ResultadoCriarConta.SenhaInicialCurta>(resultado);
    }

    [Fact]
    public async Task EmailJaCadastrado_Rejeita()
    {
        var existente = new Usuario(Guid.NewGuid(), "Outro", "a@b.com", "hash", Papel.Conferente);
        var resultado = await NovoCasoDeUso(new FakeUsuarioRepository([existente])).ExecutarAsync("A", "a@b.com", "abcdefgh", Papel.Distribuidora);
        Assert.IsType<ResultadoCriarConta.EmailJaCadastrado>(resultado);
    }
}

public class ListarContasTests
{
    [Fact]
    public async Task SoContasDeGestao_PorNome_ComTambemConfereEVoce()
    {
        var eu = new Usuario(Guid.NewGuid(), "Maria", "maria@cartorio.com", "hash", Papel.Administrador);
        var bruna = new Usuario(Guid.NewGuid(), "bruna", "bruna@cartorio.com", "hash", Papel.Distribuidora, ativo: false);
        var conferentePuro = new Usuario(Guid.NewGuid(), "Ana", "ana@cartorio.com", "hash", Papel.Conferente);
        var conferenteDaMaria = new Conferente(Guid.NewGuid(), eu.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new ListarContas(
            new FakeUsuarioRepository([eu, bruna, conferentePuro]), new FakeConferenteRepository([conferenteDaMaria]));

        var contas = await casoDeUso.ExecutarAsync(usuarioLogadoId: eu.Id);

        Assert.Equal(["bruna", "Maria"], contas.Select(c => c.Nome));
        var maria = contas.Single(c => c.Id == eu.Id);
        Assert.True(maria.TambemConfere);
        Assert.True(maria.EhVoce);
        var contaBruna = contas.Single(c => c.Id == bruna.Id);
        Assert.False(contaBruna.Ativo);
        Assert.False(contaBruna.EhVoce);
    }
}

public class DesativarContaTests
{
    private static (DesativarConta CasoDeUso, FakeProtocoloRepository Protocolos) NovoCasoDeUso(
        IReadOnlyCollection<Usuario> usuarios, IReadOnlyCollection<Conferente>? conferentes = null, IReadOnlyCollection<Protocolo>? protocolos = null)
    {
        var repoUsuarios = new FakeUsuarioRepository(usuarios);
        var repoConferentes = new FakeConferenteRepository(conferentes ?? []);
        var repoProtocolos = new FakeProtocoloRepository(protocolos ?? []);
        var unitOfWork = new FakeUnitOfWork();
        var remover = new RemoverConferente(repoConferentes, repoUsuarios, repoProtocolos, unitOfWork);
        return (new DesativarConta(repoUsuarios, repoConferentes, remover, unitOfWork), repoProtocolos);
    }

    private static Usuario Admin(string nome) => new(Guid.NewGuid(), nome, $"{nome}@cartorio.com", "hash", Papel.Administrador);

    private static Usuario Distribuidora(string nome) => new(Guid.NewGuid(), nome, $"{nome}@cartorio.com", "hash", Papel.Distribuidora);

    [Fact]
    public async Task Distribuidora_Desativa()
    {
        var eu = Admin("eu");
        var alvo = Distribuidora("alvo");
        var (casoDeUso, _) = NovoCasoDeUso([eu, alvo]);

        Assert.IsType<ResultadoDesativarConta.Sucesso>(await casoDeUso.ExecutarAsync(alvo.Id, eu.Id));
        Assert.False(alvo.Ativo);
    }

    [Fact]
    public async Task AProprioConta_Bloqueia()
    {
        var eu = Admin("eu");
        var (casoDeUso, _) = NovoCasoDeUso([eu, Admin("outro")]);

        Assert.IsType<ResultadoDesativarConta.PropriaConta>(await casoDeUso.ExecutarAsync(eu.Id, eu.Id));
        Assert.True(eu.Ativo);
    }

    [Fact]
    public async Task UltimoAdministradorAtivo_Bloqueia()
    {
        var eu = Distribuidora("eu");
        var unicoAdmin = Admin("unico");
        var adminInativo = new Usuario(Guid.NewGuid(), "velho", "velho@cartorio.com", "hash", Papel.Administrador, ativo: false);
        var (casoDeUso, _) = NovoCasoDeUso([eu, unicoAdmin, adminInativo]);

        Assert.IsType<ResultadoDesativarConta.UltimoAdministrador>(await casoDeUso.ExecutarAsync(unicoAdmin.Id, eu.Id));
        Assert.True(unicoAdmin.Ativo);
    }

    [Fact]
    public async Task APropriaContaSendoOUltimoAdmin_DizOsDoisMotivos()
    {
        var eu = Admin("eu");
        var (casoDeUso, _) = NovoCasoDeUso([eu]);

        Assert.IsType<ResultadoDesativarConta.PropriaEUltimoAdministrador>(await casoDeUso.ExecutarAsync(eu.Id, eu.Id));
    }

    [Fact]
    public async Task ContaDeConferente_Bloqueia()
    {
        var eu = Admin("eu");
        var conferente = new Usuario(Guid.NewGuid(), "c", "c@cartorio.com", "hash", Papel.Conferente);
        var (casoDeUso, _) = NovoCasoDeUso([eu, conferente]);

        Assert.IsType<ResultadoDesativarConta.ContaDeConferente>(await casoDeUso.ExecutarAsync(conferente.Id, eu.Id));
    }

    [Fact]
    public async Task ContaQueTambemConfere_SaiDaEscalaEDevolveOsAtribuidosAoPool()
    {
        var eu = Admin("eu");
        var alvo = Distribuidora("alvo");
        var conferente = new Conferente(Guid.NewGuid(), alvo.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 1);
        var protocolo = new Protocolo(Guid.NewGuid(), "1", Guid.NewGuid(), Guid.NewGuid(), Etapa.PosConferencia, DateTimeOffset.UtcNow);
        protocolo.AtribuirA(conferente.Id, DateTimeOffset.UtcNow);
        var (casoDeUso, _) = NovoCasoDeUso([eu, alvo], [conferente], [protocolo]);

        Assert.IsType<ResultadoDesativarConta.Sucesso>(await casoDeUso.ExecutarAsync(alvo.Id, eu.Id));
        Assert.False(alvo.Ativo);
        Assert.False(conferente.NaEscala);
        Assert.Equal(StatusProtocolo.Pool, protocolo.Status);
    }

    [Fact]
    public async Task JaInativa_Conflita()
    {
        var eu = Admin("eu");
        var inativa = new Usuario(Guid.NewGuid(), "x", "x@cartorio.com", "hash", Papel.Distribuidora, ativo: false);
        var (casoDeUso, _) = NovoCasoDeUso([eu, inativa]);

        Assert.IsType<ResultadoDesativarConta.JaInativa>(await casoDeUso.ExecutarAsync(inativa.Id, eu.Id));
    }
}

public class TrocarSenhaInicialTests
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    private static TrocarSenhaInicial NovoCasoDeUso(Usuario usuario) => new(
        new FakeUsuarioRepository([usuario]), new FakeConferenteRepository([]), new FakeHashDeSenha(),
        new FakeEmissorDeToken(), new FakeUnitOfWork(), new FakeRelogio(Agora));

    private static Usuario ComSenhaInicial()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Vivi", "vivi@cartorio.com", new FakeHashDeSenha().Hash("inicial-1"), Papel.Administrador);
        usuario.ExigirTrocaDeSenha();
        return usuario;
    }

    [Fact]
    public async Task SenhaForte_TrocaDesligaAPendenciaEEncerraSessoesAnteriores()
    {
        var usuario = ComSenhaInicial();

        var resultado = await NovoCasoDeUso(usuario).ExecutarAsync(usuario.Id, "inicial-1", "uma frase longa e boa");

        Assert.IsType<ResultadoTrocarSenhaInicial.Sucesso>(resultado);
        Assert.False(usuario.TrocarSenhaNoProximoAcesso);
        Assert.True(new FakeHashDeSenha().Verificar(usuario.SenhaHash, "uma frase longa e boa"));
        Assert.Equal(Agora, usuario.SessoesValidasApartirDe);
    }

    [Fact]
    public async Task SenhaAtualErrada_NaoTroca()
    {
        var usuario = ComSenhaInicial();

        var resultado = await NovoCasoDeUso(usuario).ExecutarAsync(usuario.Id, "errada", "uma frase longa e boa");

        Assert.IsType<ResultadoTrocarSenhaInicial.SenhaAtualIncorreta>(resultado);
        Assert.True(usuario.TrocarSenhaNoProximoAcesso);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("senha-bem-comprida-mas-obvia")]
    public async Task SenhaFraca_NaoTroca(string novaSenha)
    {
        var usuario = ComSenhaInicial();

        var resultado = await NovoCasoDeUso(usuario).ExecutarAsync(usuario.Id, "inicial-1", novaSenha);

        Assert.IsType<ResultadoTrocarSenhaInicial.SenhaFraca>(resultado);
    }
}
