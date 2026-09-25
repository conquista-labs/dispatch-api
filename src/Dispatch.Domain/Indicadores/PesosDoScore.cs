namespace Dispatch.Domain;

// RF-46 — "o score é 40% volume + 30% prazo + 20% qualidade + 10% complexidade, com pesos
// configuráveis". Cada peso é o MÁXIMO da sua parcela (volume e complexidade normalizados pelo melhor
// do grupo, prazo e qualidade já são frações — ver ObterDashboard). Somam exatamente 100 porque as
// faixas de bonificação (85/70) são fixas na escala 0–100: pesos somando 90 tirariam do alcance a
// faixa integral sem ninguém ter mexido nela. Inteiros ≥ 0: zero desliga uma parcela; negativo
// faria uma parcela tirar pontos.
public sealed record PesosDoScore(int Volume, int Prazo, int Qualidade, int Complexidade)
{
    public const int SomaExigida = 100;

    public static PesosDoScore Padrao { get; } = new(40, 30, 20, 10);

    public int Soma => Volume + Prazo + Qualidade + Complexidade;

    // Motivo legível (vai no 400 do PUT /config) ou null. Nomeia o campo como o JSON o chama.
    public string? Validar()
    {
        if (Volume < 0) return "pesoVolume não pode ser negativo";
        if (Prazo < 0) return "pesoPrazo não pode ser negativo";
        if (Qualidade < 0) return "pesoQualidade não pode ser negativo";
        if (Complexidade < 0) return "pesoComplexidade não pode ser negativo";
        if (Soma != SomaExigida) return $"os pesos do score precisam somar {SomaExigida} (hoje somam {Soma})";
        return null;
    }
}
