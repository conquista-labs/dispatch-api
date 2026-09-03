using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class ObterConfiguracaoTests
{
    [Fact]
    public async Task DevolveAConfiguracaoDoRepositorio()
    {
        var configuracao = new Configuracao(
            Guid.NewGuid(), TimeSpan.FromHours(4), TimeSpan.FromMinutes(60), 1, TimeSpan.FromMinutes(15),
            30, 18, 5, 8, 0.6, 3, 6, 0.5);
        var casoDeUso = new ObterConfiguracao(new FakeConfiguracaoRepository(configuracao));

        var resultado = await casoDeUso.ExecutarAsync();

        Assert.Equal(configuracao, resultado);
    }
}
