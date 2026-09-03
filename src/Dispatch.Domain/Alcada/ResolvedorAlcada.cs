namespace Dispatch.Domain;

// Motor de alçada v3 — cascata de 3 camadas (a de baixo sobrescreve a de cima quando tem
// opinião sobre o caso inteiro), reserva checada antes de qualquer camada, lista fechada por
// dimensão dentro de cada camada. Substitui o "Model A" v2 (escopo binário pessoa-ou-nível por
// família) — confirmado ao vivo contra o simulador "Testar" da aba Alçada do protótipo v2
// (Dispatch.dc.html, funções `bloqueioPuro`/`decideCamada`/`camadaDe`/`trilhaPura`, por volta
// da linha 1725). O documento de requisitos formal ainda descreve o modelo anterior — ver
// CLAUDE.md, seção "Motor de alçada v3", pra essa divergência e a decisão de documentar aqui
// em vez de editar o `.dc.html` (gerado por ferramenta externa do dono).
//
// Camadas, nesta ordem (a de baixo vence a de cima quando ambas opinam sobre o mesmo caso):
// (1) Base por nível — toda regra cujo sujeito é Nível, qualquer alvo;
// (2) Ajuste por equipe — regra de PESSOA cujo alvo é a equipe do escrevente;
// (3) Exceção por pessoa — regra de PESSOA cujo alvo não é equipe (tipo/grupo/etapa/todos).
// Dentro de uma camada: negação que bate no caso vence primeiro; senão, entre as permissões da
// camada, alçada plena basta sozinha; senão, cada dimensão (equipe/etapa/grupo/tipo, nesta
// ordem) que tiver alguma permissão na camada vira lista fechada — o caso precisa bater com
// alguma delas, senão a camada nega por omissão. Camada sem nenhuma regra aplicável não opina
// e não interfere na cascata.
// Reserva (PermissaoRegra.Reserva) é checada ANTES de qualquer camada: se existe reserva ativa
// batendo no caso e o conferente não é o sujeito dela, bloqueado direto — não concede acesso
// sozinha pro próprio sujeito, só bloqueia todo mundo mais.
public static class ResolvedorAlcada
{
    private enum Camada { Nivel, Equipe, Pessoa }

    private enum Dimensao { Equipe, Etapa, Grupo, Tipo }

    private static readonly Camada[] OrdemDasCamadas = [Camada.Nivel, Camada.Equipe, Camada.Pessoa];
    private static readonly Dimensao[] OrdemDasDimensoes = [Dimensao.Equipe, Dimensao.Etapa, Dimensao.Grupo, Dimensao.Tipo];

    public static DecisaoAlcada Resolver(Conferente conferente, CasoAlcada caso, IReadOnlyCollection<RegraAlcada> regras)
    {
        var ativas = regras.Where(r => r.Ativa).ToList();

        var reservaBloqueando = ReservaQueBloqueia(conferente, caso, ativas);
        if (reservaBloqueando is not null)
        {
            return new DecisaoAlcada(ResultadoAlcada.Negado, reservaBloqueando, MotivoAlcada.Reservado);
        }

        var minhas = ativas.Where(r => r.Permissao != PermissaoRegra.Reserva && ValePara(r, conferente)).ToList();

        // "A de baixo sobrescreve a de cima" — entre as camadas com opinião sobre o caso, a
        // última (na ordem Nível → Equipe → Pessoa) decide.
        var opinioes = CamadasComOpiniao(minhas, caso).ToList();
        if (opinioes.Count == 0)
        {
            return new DecisaoAlcada(ResultadoAlcada.Permitido, null);
        }

        var ultima = opinioes[^1];
        return ultima.Resultado == ResultadoAlcada.Negado
            ? new DecisaoAlcada(ResultadoAlcada.Negado, ultima.Regra, ultima.Dimensao is { } d ? MotivoDaDimensao(d) : MotivoAlcada.Geral)
            : new DecisaoAlcada(ResultadoAlcada.Permitido, ultima.Regra);
    }

    // Trilha completa (uma entrada por camada com opinião, mais a reserva se houver) — só pra
    // leitura explicativa (painel de detalhe, simulador "Testar"); não é chamada pelo caminho
    // quente da distribuição.
    public static IReadOnlyList<PassoTrilha> Explicar(Conferente conferente, CasoAlcada caso, IReadOnlyCollection<RegraAlcada> regras)
    {
        var ativas = regras.Where(r => r.Ativa).ToList();
        var passos = new List<PassoTrilha>();

        var reservas = ativas.Where(r => r.Permissao == PermissaoRegra.Reserva && AlvoBate(r.Alvo, caso)).ToList();
        foreach (var reserva in reservas)
        {
            passos.Add(new PassoTrilha("Reserva", ValePara(reserva, conferente) ? ResultadoAlcada.Permitido : ResultadoAlcada.Negado, reserva));
        }

        if (reservas.Count > 0 && !reservas.Any(r => ValePara(r, conferente)))
        {
            return passos;
        }

        var minhas = ativas.Where(r => r.Permissao != PermissaoRegra.Reserva && ValePara(r, conferente)).ToList();
        passos.AddRange(CamadasComOpiniao(minhas, caso).Select(o => new PassoTrilha(NomeDaCamada(o.Camada), o.Resultado, o.Regra)));

        return passos;
    }

    // Laço comum entre Resolver (reduz pra a última opinião) e Explicar (mostra todas) — cada
    // camada com regra aplicável opina uma vez, na ordem Nível → Equipe → Pessoa; camada sem
    // regra aplicável não entra (achado numa auditoria de qualidade — antes cada método
    // percorria OrdemDasCamadas por conta própria, duplicando a mesma orquestração).
    private static IEnumerable<(Camada Camada, ResultadoAlcada Resultado, RegraAlcada Regra, Dimensao? Dimensao)> CamadasComOpiniao(
        IReadOnlyCollection<RegraAlcada> minhas, CasoAlcada caso)
    {
        foreach (var camada in OrdemDasCamadas)
        {
            var doCamada = minhas.Where(r => CamadaDe(r) == camada).ToList();
            if (doCamada.Count == 0)
            {
                continue;
            }

            var (resultado, regra, dimensao) = DecideCamada(doCamada, caso);
            if (resultado is null)
            {
                continue;
            }

            yield return (camada, resultado.Value, regra!, dimensao);
        }
    }

    private static RegraAlcada? ReservaQueBloqueia(Conferente conferente, CasoAlcada caso, IReadOnlyCollection<RegraAlcada> ativas)
    {
        var reservas = ativas.Where(r => r.Permissao == PermissaoRegra.Reserva && AlvoBate(r.Alvo, caso)).ToList();
        if (reservas.Count == 0 || reservas.Any(r => ValePara(r, conferente)))
        {
            return null;
        }

        return reservas[0];
    }

    private static (ResultadoAlcada? Resultado, RegraAlcada? Regra, Dimensao? Dimensao) DecideCamada(
        IReadOnlyCollection<RegraAlcada> regrasDaCamada, CasoAlcada caso)
    {
        var negacao = regrasDaCamada.FirstOrDefault(r => r.Permissao == PermissaoRegra.Nega && AlvoBate(r.Alvo, caso));
        if (negacao is not null)
        {
            return (ResultadoAlcada.Negado, negacao, negacao.Alvo is AlvoAlcada.PorTodosOsAtos ? null : DimensaoDoAlvo(negacao.Alvo));
        }

        var permissoes = regrasDaCamada.Where(r => r.Permissao == PermissaoRegra.Permite).ToList();
        if (permissoes.Count == 0)
        {
            return (null, null, null);
        }

        var alcadaPlena = permissoes.FirstOrDefault(r => r.Alvo is AlvoAlcada.PorTodosOsAtos);
        if (alcadaPlena is not null)
        {
            return (ResultadoAlcada.Permitido, alcadaPlena, null);
        }

        foreach (var dimensao in OrdemDasDimensoes)
        {
            var permissoesDaDimensao = permissoes.Where(r => DimensaoDoAlvo(r.Alvo) == dimensao).ToList();
            if (permissoesDaDimensao.Count > 0 && !permissoesDaDimensao.Any(r => AlvoBate(r.Alvo, caso)))
            {
                return (ResultadoAlcada.Negado, permissoesDaDimensao[0], dimensao);
            }
        }

        return (ResultadoAlcada.Permitido, permissoes[0], null);
    }

    private static bool ValePara(RegraAlcada regra, Conferente conferente) => regra.Sujeito switch
    {
        SujeitoAlcada.PorPessoa pessoa => pessoa.ConferenteId == conferente.Id,
        SujeitoAlcada.PorNivel nivel => nivel.Nivel == conferente.Nivel,
        _ => throw new InvalidOperationException($"Sujeito não mapeado: {regra.Sujeito.GetType().Name}")
    };

    private static Camada CamadaDe(RegraAlcada regra) =>
        regra.Sujeito is SujeitoAlcada.PorNivel ? Camada.Nivel :
        regra.Alvo is AlvoAlcada.PorEquipeDeEscrevente ? Camada.Equipe :
        Camada.Pessoa;

    private static bool AlvoBate(AlvoAlcada alvo, CasoAlcada caso) => alvo switch
    {
        AlvoAlcada.PorTodosOsAtos => true,
        AlvoAlcada.PorEtapa porEtapa => porEtapa.Etapa == caso.Etapa,
        AlvoAlcada.PorTipoAto porTipo => porTipo.TipoAtoId == caso.TipoAto.Id,
        AlvoAlcada.PorGrupoTipoAto porGrupo => caso.TipoAto.Grupo == porGrupo.Grupo,
        AlvoAlcada.PorEquipeDeEscrevente porEquipe => porEquipe.EquipeId == caso.EquipeId,
        _ => throw new InvalidOperationException($"Alvo não mapeado: {alvo.GetType().Name}")
    };

    private static Dimensao DimensaoDoAlvo(AlvoAlcada alvo) => alvo switch
    {
        AlvoAlcada.PorEtapa => Dimensao.Etapa,
        AlvoAlcada.PorTipoAto => Dimensao.Tipo,
        AlvoAlcada.PorGrupoTipoAto => Dimensao.Grupo,
        AlvoAlcada.PorEquipeDeEscrevente => Dimensao.Equipe,
        _ => throw new InvalidOperationException($"Alvo sem dimensão própria: {alvo.GetType().Name}")
    };

    private static MotivoAlcada MotivoDaDimensao(Dimensao dimensao) => dimensao switch
    {
        Dimensao.Etapa => MotivoAlcada.Etapa,
        Dimensao.Tipo => MotivoAlcada.Tipo,
        Dimensao.Grupo => MotivoAlcada.Grupo,
        Dimensao.Equipe => MotivoAlcada.Equipe,
        _ => MotivoAlcada.Geral
    };

    private static string NomeDaCamada(Camada camada) => camada switch
    {
        Camada.Nivel => "Base por nível",
        Camada.Equipe => "Ajuste por equipe",
        Camada.Pessoa => "Exceção por pessoa",
        _ => throw new InvalidOperationException($"Camada não mapeada: {camada}")
    };
}
