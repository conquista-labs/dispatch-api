using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ListarConferentesTests
{
    [Fact]
    public async Task JuntaConferenteComNomeEEmailDoUsuario()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Márcio Gomes", "marcio@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Pleno, 8, naEscala: true, cargaAtual: 3);

        var casoDeUso = new ListarConferentes(new FakeConferenteRepository([conferente]), new FakeUsuarioRepository([usuario]));

        var resultado = await casoDeUso.ExecutarAsync();

        var item = Assert.Single(resultado);
        Assert.Equal(conferente.Id, item.Id);
        Assert.Equal("Márcio Gomes", item.Nome);
        Assert.Equal("marcio@cartorio.com", item.Email);
        Assert.True(item.Ativo);
        Assert.Equal(Nivel.Pleno, item.Nivel);
        Assert.Equal(8, item.JornadaHoras);
        Assert.True(item.NaEscala);
        Assert.Equal(3, item.CargaAtual);
        // RF-28: 8h × 60 ÷ 18min por ato = 26,67 → arredonda pra 27 (mesma fórmula do protótipo
        // aprovado e da premissa da seção 11 do documento de requisitos).
        Assert.Equal(27, item.CapacidadeEstimada);
    }

    [Fact]
    public async Task CapacidadeEstimada_NuncaFicaAbaixoDeUm()
    {
        var usuario = new Usuario(Guid.NewGuid(), "Alguém", "alguem@cartorio.com", "hash", Papel.Conferente);
        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, Nivel.Junior, jornadaHoras: 0, naEscala: false, cargaAtual: 0);

        var casoDeUso = new ListarConferentes(new FakeConferenteRepository([conferente]), new FakeUsuarioRepository([usuario]));

        var resultado = await casoDeUso.ExecutarAsync();

        Assert.Equal(1, Assert.Single(resultado).CapacidadeEstimada);
    }

    [Fact]
    public async Task SemConferentes_RetornaListaVazia()
    {
        var casoDeUso = new ListarConferentes(new FakeConferenteRepository([]), new FakeUsuarioRepository([]));

        var resultado = await casoDeUso.ExecutarAsync();

        Assert.Empty(resultado);
    }
}
