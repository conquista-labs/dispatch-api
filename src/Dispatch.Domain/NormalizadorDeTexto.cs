using System.Globalization;
using System.Text;

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

    // "É o mesmo nome?" entre o texto cru do relatório e o que já está gravado: ignora caixa,
    // acento e espaço repetido ("INVENTARIO" = "Inventário", "venda e compra" = "Venda e
    // Compra"). OrdinalIgnoreCase diferencia acento, e o relatório vem sem acento enquanto o
    // catálogo tem — cada importação criava um tipo duplicado. Serve de comparador de
    // Dictionary/SortedSet (Equals, GetHashCode e Compare coerentes entre si).
    public static StringComparer ComparadorDeNome { get; } = new ComparadorIgnorandoCaixaEAcento();

    // Forma canônica usada pelo comparador: decompõe (FormD — "á" vira "a" + acento combinante),
    // descarta as marcas combinantes, junta espaços e passa pra maiúscula invariante.
    private static string ChaveDeComparacao(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var chave = new StringBuilder(decomposto.Length);
        var espacoPendente = false;

        foreach (var caractere in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caractere) == UnicodeCategory.NonSpacingMark) continue;

            if (char.IsWhiteSpace(caractere))
            {
                espacoPendente = chave.Length > 0;
                continue;
            }

            if (espacoPendente)
            {
                chave.Append(' ');
                espacoPendente = false;
            }

            chave.Append(char.ToUpperInvariant(caractere));
        }

        return chave.ToString();
    }

    private sealed class ComparadorIgnorandoCaixaEAcento : StringComparer
    {
        public override int Compare(string? x, string? y) =>
            ReferenceEquals(x, y) ? 0
            : x is null ? -1
            : y is null ? 1
            : string.CompareOrdinal(ChaveDeComparacao(x), ChaveDeComparacao(y));

        public override bool Equals(string? x, string? y) => Compare(x, y) == 0;

        public override int GetHashCode(string obj) => ChaveDeComparacao(obj).GetHashCode(StringComparison.Ordinal);
    }
}
