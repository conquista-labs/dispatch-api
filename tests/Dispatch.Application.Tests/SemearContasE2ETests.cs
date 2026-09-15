using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class SemearContasE2ETests
{
    private static readonly FakeHashDeSenha HashDeSenha = new();
    private static readonly DateTimeOffset Agora = new(2026, 3, 5, 10, 0, 0, TimeSpan.Zero);

    private static SemearContasE2E NovoCasoDeUso(
        out FakeUsuarioRepository usuarios, out FakeConferenteRepository conferentes,
        IReadOnlyCollection<Usuario>? usuariosIniciais = null, IReadOnlyCollection<Conferente>? conferentesIniciais = null)
    {
        usuarios = new FakeUsuarioRepository(usuariosIniciais ?? []);
        conferentes = new FakeConferenteRepository(conferentesIniciais ?? []);
        return new SemearContasE2E(usuarios, conferentes, HashDeSenha, new FakeUnitOfWork(), new FakeRelogio(Agora));
    }

    [Fact]
    public async Task BancoVazio_CriaAsTresContas()
    {
        var casoDeUso = NovoCasoDeUso(out var usuarios, out var conferentes);

        await casoDeUso.ExecutarAsync();

        var distribuidora = await usuarios.ObterPorEmailAsync("distribuidora@cartorio.com", CancellationToken.None);
        Assert.NotNull(distribuidora);
        Assert.Equal(Papel.Distribuidora, distribuidora.Papel);
        Assert.True(HashDeSenha.Verificar(distribuidora.SenhaHash, SemearContasE2E.Senha));

        foreach (var email in new[] { "conferente-rf27@cartorio.com", "conferente-visual@cartorio.com" })
        {
            var usuario = await usuarios.ObterPorEmailAsync(email, CancellationToken.None);
            Assert.NotNull(usuario);
            Assert.Equal(Papel.Conferente, usuario.Papel);
            Assert.True(HashDeSenha.Verificar(usuario.SenhaHash, SemearContasE2E.Senha));

            var conferente = await conferentes.ObterPorUsuarioIdAsync(usuario.Id, CancellationToken.None);
            Assert.NotNull(conferente);
            Assert.True(conferente.NaEscala);
        }
    }

    [Fact]
    public async Task ContasJaExistem_ReSetaSenhaEDesbloqueiaSemDuplicar()
    {
        var distribuidoraExistente = new Usuario(
            Guid.NewGuid(), "Nome Antigo", "distribuidora@cartorio.com", HashDeSenha.Hash("senha-velha"), Papel.Distribuidora);
        for (var i = 0; i < 5; i++)
        {
            distribuidoraExistente.RegistrarTentativaLoginFalha(Agora);
        }
        Assert.True(distribuidoraExistente.EstaBloqueado(Agora));

        var conferenteExistente = new Usuario(
            Guid.NewGuid(), "Conferente RF27", "conferente-rf27@cartorio.com", HashDeSenha.Hash("outra-senha-velha"), Papel.Conferente);
        var conferenteAusente = new Conferente(Guid.NewGuid(), conferenteExistente.Id, Nivel.Junior, 6, naEscala: false, cargaAtual: 0);

        var casoDeUso = NovoCasoDeUso(
            out var usuarios, out var conferentes,
            usuariosIniciais: [distribuidoraExistente, conferenteExistente],
            conferentesIniciais: [conferenteAusente]);

        await casoDeUso.ExecutarAsync();

        var distribuidora = await usuarios.ObterPorEmailAsync("distribuidora@cartorio.com", CancellationToken.None);
        Assert.NotNull(distribuidora);
        Assert.Equal(distribuidoraExistente.Id, distribuidora.Id); // mesmo Id — não duplicou
        // Nome também normaliza — achado testando de verdade contra um clone de produção: a
        // conta já existia com outro nome (dono real da conta), e o teste que espera "Distribuidora
        // Teste" na tela quebrava porque só senha/bloqueio eram resetados, não o nome.
        Assert.Equal("Distribuidora Teste", distribuidora.Nome);
        Assert.True(HashDeSenha.Verificar(distribuidora.SenhaHash, SemearContasE2E.Senha));
        Assert.False(distribuidora.EstaBloqueado(Agora));

        var conferenteUsuario = await usuarios.ObterPorEmailAsync("conferente-rf27@cartorio.com", CancellationToken.None);
        Assert.NotNull(conferenteUsuario);
        Assert.Equal(conferenteExistente.Id, conferenteUsuario.Id);
        Assert.True(HashDeSenha.Verificar(conferenteUsuario.SenhaHash, SemearContasE2E.Senha));

        // Conferente marcado ausente por um teste anterior volta pra escala — a suíte não pode
        // ficar refém do que outro spec fez com a mesma conta seed antes.
        var conferente = await conferentes.ObterPorUsuarioIdAsync(conferenteExistente.Id, CancellationToken.None);
        Assert.NotNull(conferente);
        Assert.True(conferente.NaEscala);
    }
}
