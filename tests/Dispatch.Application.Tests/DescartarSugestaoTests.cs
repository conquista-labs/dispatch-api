using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class DescartarSugestaoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SugestaoPendente_Descarta()
    {
        var sugestao = new Sugestao(
            Guid.NewGuid(), "chave", new PayloadSugestao.TipoDesconhecido("X", Nivel.Pleno), "evidência", 5, 0.8, Agora);
        var casoDeUso = new DescartarSugestao(new FakeSugestaoRepository([sugestao]), new FakeUnitOfWork(), new FakeRelogio(Agora));

        var resultado = await casoDeUso.ExecutarAsync(sugestao.Id);

        Assert.True(resultado);
        Assert.Equal(StatusSugestao.Descartada, sugestao.Status);
        Assert.Equal(Agora.AddDays(30), sugestao.DescartarAte);
    }

    [Fact]
    public async Task SugestaoInexistente_RetornaFalse()
    {
        var casoDeUso = new DescartarSugestao(new FakeSugestaoRepository([]), new FakeUnitOfWork(), new FakeRelogio(Agora));

        var resultado = await casoDeUso.ExecutarAsync(Guid.NewGuid());

        Assert.False(resultado);
    }

    [Fact]
    public async Task SugestaoJaAplicada_RetornaFalse()
    {
        var sugestao = new Sugestao(
            Guid.NewGuid(), "chave", new PayloadSugestao.TipoDesconhecido("X", Nivel.Pleno), "evidência", 5, 0.8, Agora);
        sugestao.Aplicar(Agora);
        var casoDeUso = new DescartarSugestao(new FakeSugestaoRepository([sugestao]), new FakeUnitOfWork(), new FakeRelogio(Agora));

        var resultado = await casoDeUso.ExecutarAsync(sugestao.Id);

        Assert.False(resultado);
    }
}
