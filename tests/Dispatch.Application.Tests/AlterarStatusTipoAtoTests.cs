using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class AlterarStatusTipoAtoTests
{
    [Fact]
    public async Task Desativar_TipoExistente_MarcaInativo()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var tiposAto = new FakeTipoAtoRepository([tipo]);
        var casoDeUso = new DesativarTipoAto(tiposAto, new FakeUnitOfWork());

        var encontrado = await casoDeUso.ExecutarAsync(tipo.Id);

        Assert.True(encontrado);
        Assert.False(tipo.Ativo);
    }

    [Fact]
    public async Task Ativar_TipoDesativado_MarcaAtivo()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário", ativo: false);
        var tiposAto = new FakeTipoAtoRepository([tipo]);
        var casoDeUso = new AtivarTipoAto(tiposAto, new FakeUnitOfWork());

        var encontrado = await casoDeUso.ExecutarAsync(tipo.Id);

        Assert.True(encontrado);
        Assert.True(tipo.Ativo);
    }

    [Fact]
    public async Task IdInexistente_DevolveFalso()
    {
        var tiposAto = new FakeTipoAtoRepository([]);

        Assert.False(await new AtivarTipoAto(tiposAto, new FakeUnitOfWork()).ExecutarAsync(Guid.NewGuid()));
        Assert.False(await new DesativarTipoAto(tiposAto, new FakeUnitOfWork()).ExecutarAsync(Guid.NewGuid()));
    }
}
