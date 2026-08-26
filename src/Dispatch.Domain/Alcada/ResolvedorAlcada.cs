namespace Dispatch.Domain;

// Implementa a precedência da seção 4 do documento de requisitos:
// (1) regra por pessoa, quando existe para o alvo, substitui a regra por nível sobre o
//     mesmo alvo — o nível deixa de ser consultado ali;
// (2) dentro do mesmo escopo (pessoal ou nível), negação vence permissão;
// (3) ausência de regra aplicável significa permitido.
public static class ResolvedorAlcada
{
    public static DecisaoAlcada Resolver(Conferente conferente, AlvoAlcada alvo, IReadOnlyCollection<RegraAlcada> regras)
    {
        var regrasDoAlvo = regras.Where(r => r.Ativa && r.Alvo == alvo).ToList();

        var sujeitoPessoal = new SujeitoAlcada.PorPessoa(conferente.Id);
        var regrasPessoais = regrasDoAlvo.Where(r => r.Sujeito == sujeitoPessoal).ToList();
        if (regrasPessoais.Count > 0)
        {
            return ResolverDentroDoEscopo(regrasPessoais);
        }

        var sujeitoDeNivel = new SujeitoAlcada.PorNivel(conferente.Nivel);
        var regrasDeNivel = regrasDoAlvo.Where(r => r.Sujeito == sujeitoDeNivel).ToList();
        if (regrasDeNivel.Count > 0)
        {
            return ResolverDentroDoEscopo(regrasDeNivel);
        }

        return new DecisaoAlcada(ResultadoAlcada.Permitido, RegraAplicada: null);
    }

    private static DecisaoAlcada ResolverDentroDoEscopo(IReadOnlyCollection<RegraAlcada> regrasDoEscopo)
    {
        var negacao = regrasDoEscopo.FirstOrDefault(r => r.Permissao == PermissaoRegra.Nega);
        if (negacao is not null)
        {
            return new DecisaoAlcada(ResultadoAlcada.Negado, negacao);
        }

        var permissao = regrasDoEscopo.First(r => r.Permissao == PermissaoRegra.Permite);
        return new DecisaoAlcada(ResultadoAlcada.Permitido, permissao);
    }
}
