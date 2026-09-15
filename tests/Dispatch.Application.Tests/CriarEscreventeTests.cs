using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CriarEscreventeTests
{
    [Fact]
    public async Task NomeNovo_SemEquipe_CriaComSucesso()
    {
        var escreventes = new FakeEscreventeRepository([]);
        var casoDeUso = new CriarEscrevente(escreventes, new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("joão da silva", equipeId: null);

        var sucesso = Assert.IsType<ResultadoCriarEscrevente.Sucesso>(resultado);
        var escrevente = await escreventes.ObterPorIdAsync(sucesso.EscreventeId, CancellationToken.None);
        Assert.NotNull(escrevente);
        Assert.Equal("João da Silva", escrevente.Nome); // normalizado, mesmo padrão de tipo de ato
        Assert.Null(escrevente.EquipeId);
    }

    [Fact]
    public async Task ComEquipeValida_VinculaJaNaCriacao()
    {
        var equipe = new Equipe(Guid.NewGuid(), "Equipe RIO", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var escreventes = new FakeEscreventeRepository([]);
        var casoDeUso = new CriarEscrevente(escreventes, new FakeEquipeRepository([equipe]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("Maria Souza", equipe.Id);

        var sucesso = Assert.IsType<ResultadoCriarEscrevente.Sucesso>(resultado);
        var escrevente = await escreventes.ObterPorIdAsync(sucesso.EscreventeId, CancellationToken.None);
        Assert.Equal(equipe.Id, escrevente!.EquipeId);
    }

    [Fact]
    public async Task EquipeInexistente_Rejeita()
    {
        var casoDeUso = new CriarEscrevente(new FakeEscreventeRepository([]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("Maria Souza", Guid.NewGuid());

        Assert.IsType<ResultadoCriarEscrevente.EquipeNaoEncontrada>(resultado);
    }

    [Fact]
    public async Task NomeJaExiste_RejeitaCaseInsensitive()
    {
        var existente = new Escrevente(Guid.NewGuid(), "João da Silva", equipeId: null);
        var casoDeUso = new CriarEscrevente(new FakeEscreventeRepository([existente]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync("joão DA silva", equipeId: null);

        Assert.IsType<ResultadoCriarEscrevente.JaExiste>(resultado);
    }
}
