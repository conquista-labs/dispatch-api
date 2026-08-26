using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class MarcarPresencaTests
{
    [Fact]
    public async Task ConferenteExistente_AtualizaNaEscala()
    {
        var conferente = new Conferente(Guid.NewGuid(), Guid.NewGuid(), Nivel.Pleno, 8, naEscala: true, cargaAtual: 0);
        var casoDeUso = new MarcarPresenca(new FakeConferenteRepository([conferente]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(conferente.Id, presente: false);

        Assert.True(resultado);
        Assert.False(conferente.NaEscala);
    }

    [Fact]
    public async Task ConferenteInexistente_RetornaFalse()
    {
        var casoDeUso = new MarcarPresenca(new FakeConferenteRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid(), presente: false);

        Assert.False(resultado);
    }
}
