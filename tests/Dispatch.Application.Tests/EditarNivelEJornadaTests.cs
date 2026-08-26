using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class EditarNivelEJornadaTests
{
    [Fact]
    public async Task ConferenteExistente_AtualizaNivelEJornada()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Junior, 8, naEscala: true, cargaAtual: 0);
        var repositorio = new FakeConferenteRepository([conferente]);
        var casoDeUso = new EditarNivelEJornada(repositorio, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(conferente.Id, Nivel.Senior, jornadaHoras: 6);

        Assert.True(resultado);
        Assert.Equal(Nivel.Senior, conferente.Nivel);
        Assert.Equal(6, conferente.JornadaHoras);
    }

    [Fact]
    public async Task ConferenteInexistente_RetornaFalse()
    {
        var casoDeUso = new EditarNivelEJornada(new FakeConferenteRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), Nivel.Senior, jornadaHoras: 6);

        Assert.False(resultado);
    }
}
