using Dispatch.Domain;

namespace Dispatch.Application;

// PUT /config — os 12 valores são editados juntos (mesmo padrão de PUT já usado em
// Equipe/TipoAto, sem PATCH parcial). Diferente de DefinirPesoDeComplexidadeDoTipoAto (que
// clampa silenciosamente), aqui rejeita valor inválido com motivo — é uma ação deliberada da
// distribuidora editando configuração do sistema, não um valor derivado; clampar sem avisar
// esconderia o erro de digitação.
public sealed class AtualizarConfiguracao(IConfiguracaoRepository configuracao, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoAtualizarConfiguracao> ExecutarAsync(
        TimeSpan faixaAtencao, TimeSpan faixaUrgente, int limiteDeAtosSimultaneos, TimeSpan janelaDeCorrecao,
        int diasDeMemoriaDescarte, double tempoMedioPorAtoMinutos, int limiarTipoDesconhecido, int limiarPrazoIrrealCasos,
        double limiarPrazoIrrealEstouro, int limiarEscreventeOrfao, int limiarRiscoQualidadeCasos,
        double limiarRiscoQualidadeReprovacao, CancellationToken cancellationToken = default)
    {
        var motivo = Validar(
            faixaAtencao, faixaUrgente, limiteDeAtosSimultaneos, janelaDeCorrecao, diasDeMemoriaDescarte, tempoMedioPorAtoMinutos,
            limiarTipoDesconhecido, limiarPrazoIrrealCasos, limiarPrazoIrrealEstouro, limiarEscreventeOrfao, limiarRiscoQualidadeCasos,
            limiarRiscoQualidadeReprovacao);
        if (motivo is not null)
        {
            return new ResultadoAtualizarConfiguracao.ValorInvalido(motivo);
        }

        // ObterParaEdicaoAsync, não ObterAsync: precisa da instância de verdade, rastreada pelo
        // DbContext atual — ObterAsync pode devolver um objeto cacheado de uma requisição
        // anterior, que mutar+salvar aqui não persistiria (ver IConfiguracaoRepository).
        var atual = await configuracao.ObterParaEdicaoAsync(cancellationToken);
        atual.AtualizarValores(
            faixaAtencao, faixaUrgente, limiteDeAtosSimultaneos, janelaDeCorrecao, diasDeMemoriaDescarte, tempoMedioPorAtoMinutos,
            limiarTipoDesconhecido, limiarPrazoIrrealCasos, limiarPrazoIrrealEstouro, limiarEscreventeOrfao, limiarRiscoQualidadeCasos,
            limiarRiscoQualidadeReprovacao);
        await unitOfWork.SalvarAsync(cancellationToken);
        configuracao.InvalidarCache();

        return new ResultadoAtualizarConfiguracao.Sucesso();
    }

    private static string? Validar(
        TimeSpan faixaAtencao, TimeSpan faixaUrgente, int limiteDeAtosSimultaneos, TimeSpan janelaDeCorrecao,
        int diasDeMemoriaDescarte, double tempoMedioPorAtoMinutos, int limiarTipoDesconhecido, int limiarPrazoIrrealCasos,
        double limiarPrazoIrrealEstouro, int limiarEscreventeOrfao, int limiarRiscoQualidadeCasos, double limiarRiscoQualidadeReprovacao)
    {
        if (faixaAtencao <= TimeSpan.Zero) return "faixaAtencao precisa ser maior que zero";
        if (faixaUrgente <= TimeSpan.Zero) return "faixaUrgente precisa ser maior que zero";
        if (limiteDeAtosSimultaneos < 1) return "limiteDeAtosSimultaneos precisa ser pelo menos 1";
        if (janelaDeCorrecao <= TimeSpan.Zero) return "janelaDeCorrecao precisa ser maior que zero";
        if (diasDeMemoriaDescarte < 0) return "diasDeMemoriaDescarte não pode ser negativo";
        if (tempoMedioPorAtoMinutos <= 0) return "tempoMedioPorAtoMinutos precisa ser maior que zero";
        if (limiarTipoDesconhecido < 1) return "limiarTipoDesconhecido precisa ser pelo menos 1";
        if (limiarPrazoIrrealCasos < 1) return "limiarPrazoIrrealCasos precisa ser pelo menos 1";
        if (limiarPrazoIrrealEstouro is < 0 or > 1) return "limiarPrazoIrrealEstouro precisa estar entre 0 e 1";
        if (limiarEscreventeOrfao < 1) return "limiarEscreventeOrfao precisa ser pelo menos 1";
        if (limiarRiscoQualidadeCasos < 1) return "limiarRiscoQualidadeCasos precisa ser pelo menos 1";
        if (limiarRiscoQualidadeReprovacao is < 0 or > 1) return "limiarRiscoQualidadeReprovacao precisa estar entre 0 e 1";
        return null;
    }
}

public abstract record ResultadoAtualizarConfiguracao
{
    private ResultadoAtualizarConfiguracao() { }

    public sealed record Sucesso : ResultadoAtualizarConfiguracao;

    public sealed record ValorInvalido(string Motivo) : ResultadoAtualizarConfiguracao;
}
