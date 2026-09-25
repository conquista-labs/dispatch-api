namespace Dispatch.Domain;

// Seção 8 do documento de requisitos — tabela de configuração do sistema. Linha única
// (Id não é escolhido por quem cria, é sempre a mesma linha, semeada por migration): substitui
// as constantes hardcoded que várias partes do código já citavam como "até a tabela config
// existir" (faixas do semáforo, limite de atos simultâneos, janela de correção, janela de
// memória do descarte de sugestão, tempo médio por ato, e os 6 limiares do módulo de
// aprendizado). `DuracaoTipica` (GeradorDeSugestoes) fica de fora de propósito — é uma
// estrutura mapeada (TipoPrazo → TimeSpan), não um escalar, decisão consciente de não incluir
// agora.
public sealed class Configuracao
{
    public Guid Id { get; }

    // RF-14/RF-19/RF-24: as duas faixas do semáforo.
    public TimeSpan FaixaAtencao { get; private set; }
    public TimeSpan FaixaUrgente { get; private set; }

    // RF-21: quantos atos um conferente pode ter "Em conferência" ao mesmo tempo.
    public int LimiteDeAtosSimultaneos { get; private set; }

    // RF-24a: janela pra corrigir aprovado↔reprovado depois de concluído.
    public TimeSpan JanelaDeCorrecao { get; private set; }

    // RF-40: dias que uma sugestão descartada fica "esquecida" antes de poder reaparecer.
    public int DiasDeMemoriaDescarte { get; private set; }

    // RF-28: tempo médio por ato, usado no cálculo de capacidade estimada.
    public double TempoMedioPorAtoMinutos { get; private set; }

    // Seção 7 — os 6 limiares do módulo de aprendizado sem IA (GeradorDeSugestoes).
    public int LimiarTipoDesconhecido { get; private set; }
    public int LimiarPrazoIrrealCasos { get; private set; }
    public double LimiarPrazoIrrealEstouro { get; private set; }
    public int LimiarEscreventeOrfao { get; private set; }
    public int LimiarRiscoQualidadeCasos { get; private set; }
    public double LimiarRiscoQualidadeReprovacao { get; private set; }

    // RF-42b: metas do Dashboard (frações 0–1). RF-46: pesos do score (inteiros que somam 100).
    // Colunas planas (não owned type) pra seguir o resto da tabela; os tipos de valor MetasDoDashboard
    // e PesosDoScore são a leitura e a validação. Fora do construtor de propósito: nascem com os
    // padrões do protótipo e mudam só por DefinirMetasEPesos (o EF preenche pelos setters privados).
    public double MetaNoPrazo { get; private set; } = MetasDoDashboard.Padrao.NoPrazo;
    public double MetaAprovadoNaPrimeira { get; private set; } = MetasDoDashboard.Padrao.AprovadoNaPrimeira;
    public int PesoVolume { get; private set; } = PesosDoScore.Padrao.Volume;
    public int PesoPrazo { get; private set; } = PesosDoScore.Padrao.Prazo;
    public int PesoQualidade { get; private set; } = PesosDoScore.Padrao.Qualidade;
    public int PesoComplexidade { get; private set; } = PesosDoScore.Padrao.Complexidade;

    public MetasDoDashboard Metas => new(MetaNoPrazo, MetaAprovadoNaPrimeira);
    public PesosDoScore Pesos => new(PesoVolume, PesoPrazo, PesoQualidade, PesoComplexidade);

    public Configuracao(
        Guid id, TimeSpan faixaAtencao, TimeSpan faixaUrgente, int limiteDeAtosSimultaneos, TimeSpan janelaDeCorrecao,
        int diasDeMemoriaDescarte, double tempoMedioPorAtoMinutos, int limiarTipoDesconhecido, int limiarPrazoIrrealCasos,
        double limiarPrazoIrrealEstouro, int limiarEscreventeOrfao, int limiarRiscoQualidadeCasos, double limiarRiscoQualidadeReprovacao)
    {
        Id = id;
        FaixaAtencao = faixaAtencao;
        FaixaUrgente = faixaUrgente;
        LimiteDeAtosSimultaneos = limiteDeAtosSimultaneos;
        JanelaDeCorrecao = janelaDeCorrecao;
        DiasDeMemoriaDescarte = diasDeMemoriaDescarte;
        TempoMedioPorAtoMinutos = tempoMedioPorAtoMinutos;
        LimiarTipoDesconhecido = limiarTipoDesconhecido;
        LimiarPrazoIrrealCasos = limiarPrazoIrrealCasos;
        LimiarPrazoIrrealEstouro = limiarPrazoIrrealEstouro;
        LimiarEscreventeOrfao = limiarEscreventeOrfao;
        LimiarRiscoQualidadeCasos = limiarRiscoQualidadeCasos;
        LimiarRiscoQualidadeReprovacao = limiarRiscoQualidadeReprovacao;
    }

    // Editado por inteiro, não campo a campo — a Api sempre manda os 12 valores juntos (RF/UX
    // mais simples que PATCH parcial, mesmo padrão de PUT já usado em Equipe/TipoAto).
    public void AtualizarValores(
        TimeSpan faixaAtencao, TimeSpan faixaUrgente, int limiteDeAtosSimultaneos, TimeSpan janelaDeCorrecao,
        int diasDeMemoriaDescarte, double tempoMedioPorAtoMinutos, int limiarTipoDesconhecido, int limiarPrazoIrrealCasos,
        double limiarPrazoIrrealEstouro, int limiarEscreventeOrfao, int limiarRiscoQualidadeCasos, double limiarRiscoQualidadeReprovacao)
    {
        FaixaAtencao = faixaAtencao;
        FaixaUrgente = faixaUrgente;
        LimiteDeAtosSimultaneos = limiteDeAtosSimultaneos;
        JanelaDeCorrecao = janelaDeCorrecao;
        DiasDeMemoriaDescarte = diasDeMemoriaDescarte;
        TempoMedioPorAtoMinutos = tempoMedioPorAtoMinutos;
        LimiarTipoDesconhecido = limiarTipoDesconhecido;
        LimiarPrazoIrrealCasos = limiarPrazoIrrealCasos;
        LimiarPrazoIrrealEstouro = limiarPrazoIrrealEstouro;
        LimiarEscreventeOrfao = limiarEscreventeOrfao;
        LimiarRiscoQualidadeCasos = limiarRiscoQualidadeCasos;
        LimiarRiscoQualidadeReprovacao = limiarRiscoQualidadeReprovacao;
    }

    // Separado de AtualizarValores porque o PUT aceita os 6 como opcionais (ausente = mantém): quem
    // chama resolve o que veio contra os valores atuais e manda o conjunto inteiro. Valida de novo aqui
    // (a Application já validou e devolveu 400 com motivo) pra que nenhum caminho deixe a linha com
    // pesos que não somam 100 — o score inteiro dependeria disso.
    public void DefinirMetasEPesos(MetasDoDashboard metas, PesosDoScore pesos)
    {
        var motivo = metas.Validar() ?? pesos.Validar();
        if (motivo is not null)
        {
            throw new ArgumentException(motivo);
        }

        MetaNoPrazo = metas.NoPrazo;
        MetaAprovadoNaPrimeira = metas.AprovadoNaPrimeira;
        PesoVolume = pesos.Volume;
        PesoPrazo = pesos.Prazo;
        PesoQualidade = pesos.Qualidade;
        PesoComplexidade = pesos.Complexidade;
    }
}
