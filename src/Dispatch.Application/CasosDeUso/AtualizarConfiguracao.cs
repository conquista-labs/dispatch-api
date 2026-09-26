using Dispatch.Domain;

namespace Dispatch.Application;

// PUT /config — os 12 valores operacionais são editados juntos (mesmo padrão de PUT já usado em
// Equipe/TipoAto, sem PATCH parcial). As metas do Dashboard (RF-42b) e os pesos do score (RF-46) são
// a exceção: opcionais, null = mantém o atual — o front anterior salva a Configuração sem conhecê-los,
// e não pode zerar os pesos entre o deploy da API e o do front. Cada um dos 6 é resolvido sozinho
// contra o valor atual; o conjunto resultante é que precisa passar (pesos somando 100). Diferente de DefinirPesoDeComplexidadeDoTipoAto (que
// clampa silenciosamente), aqui rejeita valor inválido com motivo — é uma ação deliberada da
// distribuidora editando configuração do sistema, não um valor derivado; clampar sem avisar
// esconderia o erro de digitação.
public sealed class AtualizarConfiguracao(IConfiguracaoRepository configuracao, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoAtualizarConfiguracao> ExecutarAsync(
        TimeSpan faixaAtencao, TimeSpan faixaUrgente, int limiteDeAtosSimultaneos, TimeSpan janelaDeCorrecao,
        int diasDeMemoriaDescarte, double tempoMedioPorAtoMinutos, int limiarTipoDesconhecido, int limiarPrazoIrrealCasos,
        double limiarPrazoIrrealEstouro, int limiarEscreventeOrfao, int limiarRiscoQualidadeCasos,
        double limiarRiscoQualidadeReprovacao, double? metaNoPrazo = null, double? metaAprovadoNaPrimeira = null,
        int? pesoVolume = null, int? pesoPrazo = null, int? pesoQualidade = null, int? pesoComplexidade = null,
        int? limiteDeAtosNaMao = null, bool? poolEmOrdemObrigatoria = null,
        CancellationToken cancellationToken = default)
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

        // Regra do pool (ADR-0046): mesmo tratamento das metas — opcional, null mantém o atual.
        var limiteNaMao = limiteDeAtosNaMao ?? atual.LimiteDeAtosNaMao;
        if (Configuracao.ValidarLimiteDeAtosNaMao(limiteNaMao) is { } motivoLimite)
        {
            return new ResultadoAtualizarConfiguracao.ValorInvalido(motivoLimite);
        }

        var metas = new MetasDoDashboard(
            metaNoPrazo ?? atual.MetaNoPrazo, metaAprovadoNaPrimeira ?? atual.MetaAprovadoNaPrimeira);
        var pesos = new PesosDoScore(
            pesoVolume ?? atual.PesoVolume, pesoPrazo ?? atual.PesoPrazo, pesoQualidade ?? atual.PesoQualidade,
            pesoComplexidade ?? atual.PesoComplexidade);
        // Regra no Domain (MetasDoDashboard/PesosDoScore); aqui só vira desfecho. Antes de mutar
        // qualquer coisa — nada dos 20 valores muda quando um deles é recusado.
        var motivoMetasOuPesos = metas.Validar() ?? pesos.Validar();
        if (motivoMetasOuPesos is not null)
        {
            return new ResultadoAtualizarConfiguracao.MetasOuPesosInvalidos(motivoMetasOuPesos);
        }

        atual.AtualizarValores(
            faixaAtencao, faixaUrgente, limiteDeAtosSimultaneos, janelaDeCorrecao, diasDeMemoriaDescarte, tempoMedioPorAtoMinutos,
            limiarTipoDesconhecido, limiarPrazoIrrealCasos, limiarPrazoIrrealEstouro, limiarEscreventeOrfao, limiarRiscoQualidadeCasos,
            limiarRiscoQualidadeReprovacao);
        atual.DefinirMetasEPesos(metas, pesos);
        atual.DefinirRegraDoPool(limiteNaMao, poolEmOrdemObrigatoria ?? atual.PoolEmOrdemObrigatoria);
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
        // Regra nova (protótipo reexportado, seção 8) — as faixas contam pra trás a partir do
        // vencimento; se urgente >= atenção, o card pula direto de amarelo pra vermelho e a
        // faixa laranja (crítico) nunca aparece na prática.
        if (faixaUrgente >= faixaAtencao) return "faixaUrgente precisa ser menor que faixaAtencao — senão a faixa de urgência nunca aparece";
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

    // RF-42b/RF-46: meta fora de 0,50–1,00, peso negativo ou pesos que não somam 100.
    public sealed record MetasOuPesosInvalidos(string Motivo) : ResultadoAtualizarConfiguracao;
}
