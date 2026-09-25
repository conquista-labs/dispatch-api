namespace Dispatch.Domain;

// RF-46c: de onde vem o tempo de referência de um tipo de ato. "Historico" é a mediana da casa.
public enum OrigemTempoReferencia
{
    Informado,
    Historico,
    Estimado
}

// RF-46c + RF-34a, com as decisões do dono de 25/09/2026. Precedência: (1) o valor informado pelo
// administrador em Tipos de ato; (2) a mediana histórica da casa, quando o tipo tem pelo menos 30
// conferências válidas nos últimos 12 meses; (3) a estimativa round(TempoMedioPorAtoMinutos × peso).
// Conferências com duração acima de 4× a ESTIMATIVA ficam fora da mediana ("esquecidas abertas") —
// o requisito diz "4× a referência do tipo", mas a referência depende da própria mediana (circular) e
// um valor informado baixo descartaria o histórico real; o dono fixou a estimativa.
//
// `MedianaMinutos` é calculada mesmo quando há valor informado: o botão "usar histórico" (RF-34a) só
// faz sentido quando a mediana existe, e a tela mostra as duas. `ConferenciasNoHistorico` conta só as
// válidas (depois do descarte) — é o N de "mediana de N atos".
//
// Minutos inteiros (é o que a tela mostra e o stepper edita) e nunca abaixo de 1: a referência é
// divisor do ritmo (RF-46a).
public sealed record TempoDeReferencia(
    int Minutos,
    OrigemTempoReferencia Origem,
    int? InformadoMinutos,
    int? MedianaMinutos,
    int ConferenciasNoHistorico)
{
    public const int MinimoInformado = 2;
    public const int MaximoInformado = 240;
    public const int MinimoDeConferencias = 30;
    public const int FatorDeDescarte = 4;
    public const int MesesDeHistorico = 12;

    // Início da janela da mediana: 12 meses de calendário antes de agora (UTC — a diferença de fuso é
    // irrelevante numa janela desse tamanho).
    public static DateTimeOffset InicioDaJanela(DateTimeOffset agora) => agora.AddMonths(-MesesDeHistorico);

    // Motivo legível (400 do PUT /tipos-ato/{id}/tempo-referencia) ou null. Nulo é válido: volta a
    // usar histórico/estimativa.
    public static string? ValidarInformado(int? minutos) =>
        minutos is { } m && (m < MinimoInformado || m > MaximoInformado)
            ? $"minutos precisa estar entre {MinimoInformado} e {MaximoInformado} (ou nulo, pra voltar a usar o histórico)"
            : null;

    // "round" do dono = arredondamento escolar (22,5 → 23), não o bancário padrão do .NET (22,5 → 22).
    public static int Estimativa(double tempoMedioPorAtoMinutos, decimal peso) =>
        Math.Max(1, (int)Math.Round(tempoMedioPorAtoMinutos * (double)peso, MidpointRounding.AwayFromZero));

    public static TempoDeReferencia Calcular(
        int? informadoMinutos, decimal peso, double tempoMedioPorAtoMinutos, IEnumerable<TimeSpan> duracoesNaJanela)
    {
        var estimativa = Estimativa(tempoMedioPorAtoMinutos, peso);
        var limite = TimeSpan.FromMinutes(estimativa * FatorDeDescarte);
        var validas = duracoesNaJanela.Where(d => d <= limite).OrderBy(d => d).ToList();
        int? mediana = validas.Count >= MinimoDeConferencias ? MedianaEmMinutos(validas) : null;

        var (minutos, origem) = (informadoMinutos, mediana) switch
        {
            ({ } informado, _) => (informado, OrigemTempoReferencia.Informado),
            (null, { } m) => (m, OrigemTempoReferencia.Historico),
            _ => (estimativa, OrigemTempoReferencia.Estimado),
        };

        return new TempoDeReferencia(minutos, origem, informadoMinutos, mediana, validas.Count);
    }

    // Lista já ordenada. Par: média dos dois do meio.
    private static int MedianaEmMinutos(IReadOnlyList<TimeSpan> ordenadas)
    {
        var meio = ordenadas.Count / 2;
        var minutos = ordenadas.Count % 2 == 1
            ? ordenadas[meio].TotalMinutes
            : (ordenadas[meio - 1].TotalMinutes + ordenadas[meio].TotalMinutes) / 2;
        return Math.Max(1, (int)Math.Round(minutos, MidpointRounding.AwayFromZero));
    }
}

// Recorte leve de uma conferência concluída — só o que a mediana precisa. Existe pra que o histórico
// de 12 meses venha numa query projetada, sem materializar Protocolo com as coleções filhas.
public sealed record DuracaoDeConferencia(Guid TipoAtoId, TimeSpan Duracao);
