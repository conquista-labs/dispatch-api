namespace Dispatch.Application.Tests;

public class AtualizarConfiguracaoTests
{
    private static AtualizarConfiguracao NovoCasoDeUso(out FakeConfiguracaoRepository configuracao)
    {
        configuracao = new FakeConfiguracaoRepository();
        return new AtualizarConfiguracao(configuracao, new FakeUnitOfWork());
    }

    [Fact]
    public async Task ValoresValidos_AtualizaEDevolveSucesso()
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);

        var resultado = await casoDeUso.ExecutarAsync(
            TimeSpan.FromHours(2), TimeSpan.FromMinutes(30), 2, TimeSpan.FromMinutes(20), 15, 20, 4, 7, 0.5, 2, 5, 0.4);

        Assert.IsType<ResultadoAtualizarConfiguracao.Sucesso>(resultado);
        var atual = await configuracao.ObterAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.FromHours(2), atual.FaixaAtencao);
        Assert.Equal(2, atual.LimiteDeAtosSimultaneos);
        Assert.Equal(0.4, atual.LimiarRiscoQualidadeReprovacao);
    }

    [Theory]
    [InlineData(0, 60, 1, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // faixaAtencao zero
    [InlineData(240, 60, 0, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // limiteDeAtosSimultaneos < 1
    [InlineData(240, 60, 1, 15, -1, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // diasDeMemoriaDescarte negativo
    [InlineData(240, 60, 1, 15, 30, 18.0, 5, 8, 1.5, 3, 6, 0.5)] // limiarPrazoIrrealEstouro fora de 0-1
    [InlineData(60, 60, 1, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // faixaUrgente igual a faixaAtencao
    [InlineData(60, 240, 1, 15, 30, 18.0, 5, 8, 0.6, 3, 6, 0.5)] // faixaUrgente maior que faixaAtencao
    public async Task ValorInvalido_RejeitaSemAlterarNada(
        int faixaAtencaoMinutos, int faixaUrgenteMinutos, int limiteDeAtosSimultaneos, int janelaDeCorrecaoMinutos,
        int diasDeMemoriaDescarte, double tempoMedioPorAtoMinutos, int limiarTipoDesconhecido, int limiarPrazoIrrealCasos,
        double limiarPrazoIrrealEstouro, int limiarEscreventeOrfao, int limiarRiscoQualidadeCasos, double limiarRiscoQualidadeReprovacao)
    {
        var casoDeUso = NovoCasoDeUso(out var configuracao);
        var original = await configuracao.ObterAsync(CancellationToken.None);
        var faixaAtencaoOriginal = original.FaixaAtencao;

        var resultado = await casoDeUso.ExecutarAsync(
            TimeSpan.FromMinutes(faixaAtencaoMinutos), TimeSpan.FromMinutes(faixaUrgenteMinutos), limiteDeAtosSimultaneos,
            TimeSpan.FromMinutes(janelaDeCorrecaoMinutos), diasDeMemoriaDescarte, tempoMedioPorAtoMinutos, limiarTipoDesconhecido,
            limiarPrazoIrrealCasos, limiarPrazoIrrealEstouro, limiarEscreventeOrfao, limiarRiscoQualidadeCasos,
            limiarRiscoQualidadeReprovacao);

        Assert.IsType<ResultadoAtualizarConfiguracao.ValorInvalido>(resultado);
        Assert.Equal(faixaAtencaoOriginal, original.FaixaAtencao);
    }
}
