namespace Dispatch.Domain;

// Implementa a precedência da seção 4 do documento de requisitos, revisada (v2 do protótipo,
// confirmada ao vivo contra a ferramenta "Testar" da aba Alçada — ver CLAUDE.md):
// (1) regra por pessoa, quando toca a mesma família de alvo (etapa / tipo / equipe), substitui
//     TODAS as regras de nível daquela família — o nível deixa de ser consultado ali, mesmo
//     pra alvos que a regra pessoal não cobre especificamente (é o que permite alçada plena
//     pessoal vencer uma negação de nível: ver passo 3 do escopo);
// (2) dentro do mesmo escopo, negação específica vence permissão específica;
// (3) alçada plena (só família Tipo) — permite qualquer tipo, cede a uma negação específica
//     do mesmo escopo;
// (4) lista fechada: se o escopo definiu qualquer Permite naquela família (sem cobrir o alvo
//     consultado), o alvo fica bloqueado por omissão — "fora da alçada";
// (5) ausência de qualquer regra do escopo naquela família (ou só negações de outros alvos)
//     significa permitido — padrão aberto.
public static class ResolvedorAlcada
{
    public static DecisaoAlcada Resolver(Conferente conferente, AlvoAlcada alvo, IReadOnlyCollection<RegraAlcada> regras)
    {
        var familia = FamiliaDoAlvo(alvo);

        var sujeitoPessoal = new SujeitoAlcada.PorPessoa(conferente.Id);
        var regrasPessoaisDaFamilia = regras.Where(r => r.Ativa && r.Sujeito == sujeitoPessoal && FamiliaDoAlvo(r.Alvo) == familia).ToList();
        if (regrasPessoaisDaFamilia.Count > 0)
        {
            return ResolverDentroDoEscopo(regrasPessoaisDaFamilia, alvo, familia);
        }

        var sujeitoDeNivel = new SujeitoAlcada.PorNivel(conferente.Nivel);
        var regrasDeNivelDaFamilia = regras.Where(r => r.Ativa && r.Sujeito == sujeitoDeNivel && FamiliaDoAlvo(r.Alvo) == familia).ToList();
        if (regrasDeNivelDaFamilia.Count > 0)
        {
            return ResolverDentroDoEscopo(regrasDeNivelDaFamilia, alvo, familia);
        }

        return new DecisaoAlcada(ResultadoAlcada.Permitido, RegraAplicada: null);
    }

    private static DecisaoAlcada ResolverDentroDoEscopo(IReadOnlyCollection<RegraAlcada> regrasDoEscopo, AlvoAlcada alvo, FamiliaAlvo familia)
    {
        var negacaoEspecifica = regrasDoEscopo.FirstOrDefault(r => r.Permissao == PermissaoRegra.Nega && r.Alvo == alvo);
        if (negacaoEspecifica is not null)
        {
            return new DecisaoAlcada(ResultadoAlcada.Negado, negacaoEspecifica);
        }

        var permissaoEspecifica = regrasDoEscopo.FirstOrDefault(r => r.Permissao == PermissaoRegra.Permite && r.Alvo == alvo);
        if (permissaoEspecifica is not null)
        {
            return new DecisaoAlcada(ResultadoAlcada.Permitido, permissaoEspecifica);
        }

        if (familia == FamiliaAlvo.Tipo)
        {
            var alcadaPlena = regrasDoEscopo.FirstOrDefault(r => r.Permissao == PermissaoRegra.Permite && r.Alvo is AlvoAlcada.PorTodosOsAtos);
            if (alcadaPlena is not null)
            {
                return new DecisaoAlcada(ResultadoAlcada.Permitido, alcadaPlena);
            }
        }

        var qualquerPermissaoNaFamilia = regrasDoEscopo.FirstOrDefault(r => r.Permissao == PermissaoRegra.Permite);
        if (qualquerPermissaoNaFamilia is not null)
        {
            return new DecisaoAlcada(ResultadoAlcada.Negado, qualquerPermissaoNaFamilia);
        }

        return new DecisaoAlcada(ResultadoAlcada.Permitido, RegraAplicada: null);
    }

    private enum FamiliaAlvo { Etapa, Tipo, Equipe }

    private static FamiliaAlvo FamiliaDoAlvo(AlvoAlcada alvo) => alvo switch
    {
        AlvoAlcada.PorEtapa => FamiliaAlvo.Etapa,
        AlvoAlcada.PorTipoAto or AlvoAlcada.PorTodosOsAtos => FamiliaAlvo.Tipo,
        AlvoAlcada.PorEquipeDeEscrevente => FamiliaAlvo.Equipe,
        _ => throw new InvalidOperationException($"Alvo não mapeado: {alvo.GetType().Name}")
    };
}
