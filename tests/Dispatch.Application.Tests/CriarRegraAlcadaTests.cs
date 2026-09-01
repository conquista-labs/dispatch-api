using Dispatch.Domain;

namespace Dispatch.Application.Tests;

public class CriarRegraAlcadaTests
{
    [Fact]
    public async Task RegraPorNivelETipoConhecido_Cria()
    {
        var tipo = new TipoAto(Guid.NewGuid(), "Inventário");
        var regras = new FakeRegraAlcadaRepository([]);
        var casoDeUso = new CriarRegraAlcada(
            regras, new FakeConferenteRepository([]), new FakeTipoAtoRepository([tipo]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Junior), PermissaoRegra.Nega, new AlvoAlcada.PorTipoAto(tipo.Id));

        Assert.IsType<ResultadoCriarRegraAlcada.Sucesso>(resultado);
        Assert.Equal(1, regras.Quantidade);
    }

    [Fact]
    public async Task SujeitoPorPessoaComConferenteInexistente_Rejeita()
    {
        var casoDeUso = new CriarRegraAlcada(
            new FakeRegraAlcadaRepository([]), new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorPessoa(Guid.NewGuid()), PermissaoRegra.Permite, new AlvoAlcada.PorEtapa(Etapa.PreConferencia));

        Assert.IsType<ResultadoCriarRegraAlcada.ConferenteNaoEncontrado>(resultado);
    }

    [Fact]
    public async Task AlvoPorTipoAtoInexistente_Rejeita()
    {
        var casoDeUso = new CriarRegraAlcada(
            new FakeRegraAlcadaRepository([]), new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Pleno), PermissaoRegra.Permite, new AlvoAlcada.PorTipoAto(Guid.NewGuid()));

        Assert.IsType<ResultadoCriarRegraAlcada.TipoAtoNaoEncontrado>(resultado);
    }

    [Fact]
    public async Task AlvoPorEquipeExistente_Cria()
    {
        var equipe = new Equipe(Guid.NewGuid(), "Balcão", new Prazo(TipoPrazo.D1), new Prazo(TipoPrazo.D1));
        var regras = new FakeRegraAlcadaRepository([]);
        var casoDeUso = new CriarRegraAlcada(
            regras, new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeEquipeRepository([equipe]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Permite, new AlvoAlcada.PorEquipeDeEscrevente(equipe.Id));

        Assert.IsType<ResultadoCriarRegraAlcada.Sucesso>(resultado);
        Assert.Equal(1, regras.Quantidade);
    }

    [Fact]
    public async Task AlvoPorEquipeSemEquipe_NaoPrecisaValidarReferencia()
    {
        // Guid? nulo é "sem equipe" — alvo válido por si só (RF-29a), não é ausência de dado.
        var regras = new FakeRegraAlcadaRepository([]);
        var casoDeUso = new CriarRegraAlcada(
            regras, new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Permite, new AlvoAlcada.PorEquipeDeEscrevente(null));

        Assert.IsType<ResultadoCriarRegraAlcada.Sucesso>(resultado);
    }

    [Fact]
    public async Task AlvoPorEquipeInexistente_Rejeita()
    {
        var casoDeUso = new CriarRegraAlcada(
            new FakeRegraAlcadaRepository([]), new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Pleno), PermissaoRegra.Permite, new AlvoAlcada.PorEquipeDeEscrevente(Guid.NewGuid()));

        Assert.IsType<ResultadoCriarRegraAlcada.EquipeNaoEncontrada>(resultado);
    }

    [Fact]
    public async Task AlvoTodosOsAtos_NaoPrecisaValidarReferencia()
    {
        var regras = new FakeRegraAlcadaRepository([]);
        var casoDeUso = new CriarRegraAlcada(
            regras, new FakeConferenteRepository([]), new FakeTipoAtoRepository([]), new FakeEquipeRepository([]), new FakeUnitOfWork());

        var resultado = await casoDeUso.ExecutarAsync(
            new SujeitoAlcada.PorNivel(Nivel.Senior), PermissaoRegra.Permite, new AlvoAlcada.PorTodosOsAtos());

        Assert.IsType<ResultadoCriarRegraAlcada.Sucesso>(resultado);
        Assert.Equal(1, regras.Quantidade);
    }
}
