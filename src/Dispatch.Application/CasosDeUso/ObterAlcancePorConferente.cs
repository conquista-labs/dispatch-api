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
// verdade combinatória completa.
// - "Tipos permitidos": o tipo conta se é permitido em PELO MENOS UMA etapa (Pré ou Pós), com
//   equipe=null. Antes fixava só Etapa=PosConferencia — achado em produção: uma conferente Pleno
//   com a regra própria "Nega etapa PosConferencia" (só faz pré, todo dia) aparecia com 0 tipos e
//   a tela dizia que ela "não recebe nenhum ato"; o mesmo falso negativo vazava pra cobertura
//   (RF-30) e pro "N com alçada" dos Tipos (RF-34a), que reaproveitam esta lista.
// - "Etapas permitidas": fixa um tipo representativo (o primeiro que a pessoa alcança em alguma
//   etapa, senão o primeiro do catálogo) e equipe=null.
// - "Equipes permitidas": fixa esse mesmo tipo representativo e a PRIMEIRA etapa que a pessoa tem
//   liberada (senão PosConferencia) — pelo mesmo motivo: avaliar sempre em Pós deixava quem só faz
//   pré sem equipe nenhuma.
// Não é mais exato do que isso — documentado aqui em vez de fingir precisão que o modelo novo não
// garante mais.
public sealed class ObterAlcancePorConferente(
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IEquipeRepository equipes)
{
    private static readonly Etapa[] TodasAsEtapas = [Etapa.PreConferencia, Etapa.PosConferencia];
    // Etapa usada pra avaliar equipes quando a pessoa não tem etapa nenhuma liberada (não há
    // "primeira etapa liberada" pra usar) — mantém o comportamento anterior pra esse caso.
    private const Etapa EtapaPadraoParaEquipes = Etapa.PosConferencia;

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
                .Where(tipo => TodasAsEtapas.Any(etapa => EhPermitido(conferente, new CasoAlcada(etapa, tipo, null), regrasAtivas)))
                .Select(tipo => tipo.Id)
                .ToList();

            // Tipo representativo pra checar etapa/equipe: o primeiro que a pessoa já alcança
            // em alguma etapa (lista acima), senão o primeiro do catálogo (mesma escolha do simulador do
            // protótipo — sem isso, alguém sem nenhum tipo liberado ficaria sem
            // como testar etapa/equipe de jeito nenhum).
            var tipoRepresentativo = catalogoTipos.FirstOrDefault(t => tiposPermitidos.Contains(t.Id)) ?? catalogoTipos[0];

            var etapasPermitidas = TodasAsEtapas
                .Where(etapa => EhPermitido(conferente, new CasoAlcada(etapa, tipoRepresentativo, null), regrasAtivas))
                .ToList();

            var etapaParaEquipes = etapasPermitidas.Count > 0 ? etapasPermitidas[0] : EtapaPadraoParaEquipes;
            var equipesPermitidas = todasAsEquipesIds
                .Where(equipeId => EhPermitido(conferente, new CasoAlcada(etapaParaEquipes, tipoRepresentativo, equipeId), regrasAtivas))
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
