using Dispatch.Domain;

namespace Dispatch.Application;

// RF-34: painel de alcance de cada pessoa. Reaproveita ResolvedorAlcada — a mesma resolução
// que o motor usa pra decidir quem confere o quê, aqui só reportando o que cada conferente
// alcança hoje, sem envolver protocolo nenhum.
//
// Simplificação consciente do motor v3: uma regra de equipe pode depender da COMBINAÇÃO
// etapa+tipo+equipe (ver ResolvedorAlcada — a cascata resolve o caso inteiro, não 3 eixos
// independentes), então "quantos tipos alcança" deixou de ser um fato puro por tipo. Adoto a
// mesma aproximação já usada pelo RF-30/cobertura (`ObterCoberturaDeAlcada`) e pelo próprio
// simulador do protótipo: fixa um CASO REPRESENTATIVO por eixo em vez de tentar reportar uma
// verdade combinatória completa. "Tipos permitidos" fixa Etapa=PosConferencia e equipe=null;
// "etapas permitidas" fixa um tipo representativo (o primeiro que a pessoa já alcança nesse
// caso-base, senão o primeiro do catálogo); "equipes permitidas" fixa esse mesmo tipo
// representativo e a etapa PosConferencia. Não é mais exato do que isso — documentado aqui em
// vez de fingir precisão que o modelo novo não garante mais.
public sealed class ObterAlcancePorConferente(
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IEquipeRepository equipes)
{
    private static readonly Etapa[] TodasAsEtapas = [Etapa.PreConferencia, Etapa.PosConferencia];
    private const Etapa EtapaBase = Etapa.PosConferencia;

    public async Task<IReadOnlyList<AlcanceDoConferente>> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var todosConferentes = await conferentes.ObterTodosAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var catalogoTipos = (await tiposAto.ObterTodosAsync(cancellationToken)).ToList();
        var todasAsEquipes = await equipes.ObterTodasAsync(cancellationToken);
        // "sem equipe" (null) é um alvo válido de regra (RF-29a) — entra na lista igual a
        // qualquer outra equipe, representado aqui por um Guid? nulo.
        var todasAsEquipesIds = todasAsEquipes.Select(e => (Guid?)e.Id).Append(null).ToList();

        if (catalogoTipos.Count == 0)
        {
            return todosConferentes.Select(c => new AlcanceDoConferente(c.Id, [], [], [])).ToList();
        }

        return todosConferentes.Select(conferente =>
        {
            var tiposPermitidos = catalogoTipos
                .Where(tipo => EhPermitido(conferente, new CasoAlcada(EtapaBase, tipo, null), regrasAtivas))
                .Select(tipo => tipo.Id)
                .ToList();

            // Tipo representativo pra checar etapa/equipe: o primeiro que a pessoa já alcança
            // no caso-base acima, senão o primeiro do catálogo (mesma escolha do simulador do
            // protótipo — sem isso, alguém sem nenhum tipo liberado no caso-base ficaria sem
            // como testar etapa/equipe de jeito nenhum).
            var tipoRepresentativo = catalogoTipos.FirstOrDefault(t => tiposPermitidos.Contains(t.Id)) ?? catalogoTipos[0];

            var etapasPermitidas = TodasAsEtapas
                .Where(etapa => EhPermitido(conferente, new CasoAlcada(etapa, tipoRepresentativo, null), regrasAtivas))
                .ToList();

            var equipesPermitidas = todasAsEquipesIds
                .Where(equipeId => EhPermitido(conferente, new CasoAlcada(EtapaBase, tipoRepresentativo, equipeId), regrasAtivas))
                .ToList();

            return new AlcanceDoConferente(conferente.Id, etapasPermitidas, tiposPermitidos, equipesPermitidas);
        }).ToList();
    }

    private static bool EhPermitido(Conferente conferente, CasoAlcada caso, IReadOnlyCollection<RegraAlcada> regras) =>
        ResolvedorAlcada.Resolver(conferente, caso, regras).Resultado == ResultadoAlcada.Permitido;
}

public sealed record AlcanceDoConferente(
    Guid ConferenteId, IReadOnlyList<Etapa> EtapasPermitidas, IReadOnlyList<Guid> TiposPermitidosIds,
    IReadOnlyList<Guid?> EquipesPermitidasIds);
