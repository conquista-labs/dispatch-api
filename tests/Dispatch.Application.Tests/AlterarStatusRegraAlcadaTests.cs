using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class AlterarStatusRegraAlcadaTests
{
    private static RegraAlcada NovaRegra() =>
        new(Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

    [Fact]
    public async Task Desativar_RegraExistente_Desativa()
    {
        var regra = NovaRegra();
        var repositorio = new FakeRegraAlcadaRepository([regra]);
        var casoDeUso = new DesativarRegraAlcada(repositorio, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(regra.Id);

        Assert.True(resultado);
        Assert.False(regra.Ativa);
    }

    [Fact]
    public async Task Ativar_RegraExistente_Ativa()
    {
        var regra = NovaRegra();
        regra.Desativar();
        var repositorio = new FakeRegraAlcadaRepository([regra]);
        var casoDeUso = new AtivarRegraAlcada(repositorio, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(regra.Id);

        Assert.True(resultado);
        Assert.True(regra.Ativa);
    }

    [Fact]
    public async Task Remover_RegraExistente_SomeDoRepositorio()
    {
        var regra = NovaRegra();
        var repositorio = new FakeRegraAlcadaRepository([regra]);
        var casoDeUso = new RemoverRegraAlcada(repositorio, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(regra.Id);

        Assert.True(resultado);
        Assert.Equal(0, repositorio.Quantidade);
    }

    [Fact]
    public async Task RegraInexistente_RetornaFalse()
    {
        var repositorio = new FakeRegraAlcadaRepository([]);
        var casoDeUso = new DesativarRegraAlcada(repositorio, new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.False(resultado);
    }
}
