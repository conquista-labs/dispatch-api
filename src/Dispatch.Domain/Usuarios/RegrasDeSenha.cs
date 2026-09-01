namespace Dispatch.Domain;

// RF-01j: mesmas 3 regras ao vivo do protótipo (Dispatch.dc.html, `recVals()`, passo "senha") —
// o front replica isso pra feedback imediato, mas aqui é a fonte de verdade (nunca confiar só
// na validação de UI pra uma troca de senha). Comprimento em vez de complexidade: "frase longa
// e fácil de lembrar... comprimento protege mais que símbolos", texto literal do protótipo.
public static class RegrasDeSenha
{
    private static readonly string[] PrefixosObvios = ["senha", "123", "cartorio", "dispatch"];

    public static bool TemComprimentoMinimo(string senha) => senha.Length >= 12;

    public static bool NaoEhObvia(string senha) =>
        TemComprimentoMinimo(senha) &&
        !PrefixosObvios.Any(prefixo => senha.StartsWith(prefixo, StringComparison.OrdinalIgnoreCase));

    public static bool EhForte(string senha) => TemComprimentoMinimo(senha) && NaoEhObvia(senha);
}
