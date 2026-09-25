namespace Dispatch.Domain;

// RF-34f/RF-46: peso de complexidade de um tipo de ato — decimal de 0,50 a 2,50 em passos de 0,05
// (decisão 2 do dono, 25/09/2026, igual ao protótipo v2). Até a fatia 5 do Dashboard v2 era um inteiro
// ≥ 1 sem teto; a migration ConverteTempoDeReferenciaEPesoDecimalEmTiposAto converteu 1→1,00,
// 2→1,25, 3→1,50, 4→1,75, 5→2,00 (fora disso, clamp na faixa). Alimenta a parcela de complexidade do
// score e a estimativa do tempo de referência (TempoDeReferencia.Estimativa).
public static class PesoDeComplexidade
{
    public const decimal Minimo = 0.50m;
    public const decimal Maximo = 2.50m;
    public const decimal Passo = 0.05m;
    public const decimal Padrao = 1.00m;

    // Motivo legível (vai no 400 do PUT /tipos-ato/{id}/peso) ou null.
    public static string? Validar(decimal peso)
    {
        if (peso < Minimo || peso > Maximo)
        {
            return "o peso de complexidade precisa estar entre 0,50 e 2,50";
        }

        // decimal é exato em base 10: 1,35 % 0,05 é 0 de verdade (em double não seria).
        return peso % Passo == 0 ? null : "o peso de complexidade precisa ser múltiplo de 0,05";
    }
}
