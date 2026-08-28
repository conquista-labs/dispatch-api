using System.Globalization;

namespace Dispatch.Domain;

// O relatório do cartório chega em CAIXA ALTA (sistema de origem) — não é como nome de pessoa
// ou de tipo de ato deveria ficar gravado. Usado sempre que a importação cadastra algo novo no
// banco a partir do texto cru do relatório (RF-09: escrevente novo; e o cadastro automático de
// tipo de ato novo — ver ImportarLote). Só maiúscula/minúscula, não tenta resolver abreviação
// nem capitalização de sigla (ex.: "LTDA" vira "Ltda", aceito).
public static class NormalizadorDeTexto
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    // Conectivos usuais de nome próprio brasileiro ficam minúsculos, exceto na primeira palavra
    // ("Ana Beatriz da Silva Oliveira", "Venda e Compra", não "Ana Beatriz Da Silva Oliveira").
    private static readonly HashSet<string> Conectivos = new(StringComparer.OrdinalIgnoreCase) { "de", "da", "do", "das", "dos", "e" };

    public static string ParaNomeProprio(string texto)
    {
        var palavras = texto.Trim().ToLower(PtBr).Split(' ', StringSplitOptions.RemoveEmptyEntries);

        for (var i = 0; i < palavras.Length; i++)
        {
            if (i > 0 && Conectivos.Contains(palavras[i])) continue;
            palavras[i] = CapitalizarPrimeiraLetra(palavras[i]);
        }

        return string.Join(' ', palavras);
    }

    private static string CapitalizarPrimeiraLetra(string palavra) =>
        palavra.Length == 0 ? palavra : char.ToUpper(palavra[0], PtBr) + palavra[1..];
}
