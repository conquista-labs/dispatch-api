namespace Dispatch.Domain;

// RF-42b — "Dentro do prazo" e "Aprovados na 1ª" têm barra com a meta marcada (padrão 95% e 90%,
// configuráveis em Configuração do sistema). Frações 0–1, como todo percentual do JSON do Dashboard.
// A faixa 0,50–1,00 é leitura adotada (o requisito não dá limites): abaixo de 50% a meta deixa de
// separar bom de ruim, e acima de 100% nunca seria atingida. Só a gestão vê a meta (decisão 4 do
// dono) — quem decide isso é ObterDashboard, não este tipo.
public sealed record MetasDoDashboard(double NoPrazo, double AprovadoNaPrimeira)
{
    public const double Minima = 0.50;
    public const double Maxima = 1.00;

    public static MetasDoDashboard Padrao { get; } = new(0.95, 0.90);

    // Motivo legível (vai no 400 do PUT /config) ou null. Nomeia o campo como o JSON o chama.
    public string? Validar()
    {
        if (!DentroDaFaixa(NoPrazo)) return "metaNoPrazo precisa estar entre 0,50 e 1,00 (fração, não percentual)";
        if (!DentroDaFaixa(AprovadoNaPrimeira)) return "metaAprovadoNaPrimeira precisa estar entre 0,50 e 1,00 (fração, não percentual)";
        return null;
    }

    // Comparação que também recusa NaN (NaN falha nas duas pontas).
    private static bool DentroDaFaixa(double meta) => meta is >= Minima and <= Maxima;
}
