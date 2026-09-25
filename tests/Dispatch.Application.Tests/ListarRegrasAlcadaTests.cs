using Dispatch.Domain;

namespace Dispatch.Application.Tests;

// ADR-0039 / RF-30a: a distribuidora lê as regras em vigor sem ficar sabendo o cargo de ninguém.
public class ListarRegrasAlcadaTests
{
    private static readonly RegraAlcada RegraDeNivel = new(
        Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

    private static readonly RegraAlcada RegraDePessoa = new(
        Guid.NewGuid(), new SujeitoAlcada.PorPessoa(Guid.NewGuid()), PermissaoRegra.Permite, new AlvoAlcada.PorTodosOsAtos());

    private static readonly RegraAlcada EquipeNaoFazEtapa = new(
        Guid.NewGuid(), new SujeitoAlcada.PorNivel(Nivel.Pleno), PermissaoRegra.Nega,
        new AlvoAlcada.PorEquipeEEtapa(Guid.NewGuid(), Etapa.PreConferencia));

    private static ListarRegrasAlcada NovoCasoDeUso() =>
        new(new FakeRegraAlcadaRepository([RegraDeNivel, RegraDePessoa, EquipeNaoFazEtapa]));

    [Fact]
    public async Task SemFlagDeAdministrador_EscondeONivelEMarcaRegraBase()
    {
        var regras = (await NovoCasoDeUso().ExecutarAsync(incluirNivel: false)).ToDictionary(r => r.Regra.Id);

        Assert.True(regras[RegraDeNivel.Id].NivelOculto);
        Assert.True(regras[RegraDeNivel.Id].RegraBase);

        Assert.False(regras[RegraDePessoa.Id].NivelOculto);
        Assert.False(regras[RegraDePessoa.Id].RegraBase);

        // "Equipe X não faz etapa": esconde o nível (é gravada por nível), mas não é "regra base".
        Assert.True(regras[EquipeNaoFazEtapa.Id].NivelOculto);
        Assert.False(regras[EquipeNaoFazEtapa.Id].RegraBase);
    }

    [Fact]
    public async Task ComFlagDeAdministrador_NadaFicaOculto()
    {
        var regras = await NovoCasoDeUso().ExecutarAsync(incluirNivel: true);

        Assert.All(regras, r =>
        {
            Assert.False(r.NivelOculto);
            Assert.False(r.RegraBase);
        });
    }
}
