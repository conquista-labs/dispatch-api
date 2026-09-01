using Dispatch.Domain;

namespace Dispatch.Application;

// RF-34: painel de alcance de cada pessoa. Reaproveita ResolvedorAlcada — a mesma resolução
// de precedência que o motor usa pra decidir quem confere o quê, aqui só reportando o que
// cada conferente alcança hoje, sem envolver protocolo nenhum.
public sealed class ObterAlcancePorConferente(
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IEquipeRepository equipes)
{
    private static readonly Etapa[] TodasAsEtapas = [Etapa.PreConferencia, Etapa.PosConferencia];

    public async Task<IReadOnlyList<AlcanceDoConferente>> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var todosConferentes = await conferentes.ObterTodosAsync(cancellationToken);
        var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
        var catalogoTipos = await tiposAto.ObterTodosAsync(cancellationToken);
        var todasAsEquipes = await equipes.ObterTodasAsync(cancellationToken);
        // "sem equipe" (null) é um alvo válido de regra (RF-29a) — entra na lista igual a
        // qualquer outra equipe, representado aqui por um Guid? nulo.
        var todasAsEquipesIds = todasAsEquipes.Select(e => (Guid?)e.Id).Append(null).ToList();

        return todosConferentes.Select(conferente =>
        {
            var etapasPermitidas = TodasAsEtapas
                .Where(etapa => EhPermitido(conferente, new AlvoAlcada.PorEtapa(etapa), regrasAtivas))
                .ToList();

            var tiposPermitidos = catalogoTipos
                .Where(tipo => EhPermitido(conferente, new AlvoAlcada.PorTipoAto(tipo.Id), regrasAtivas))
                .Select(tipo => tipo.Id)
                .ToList();

            var equipesPermitidas = todasAsEquipesIds
                .Where(equipeId => EhPermitido(conferente, new AlvoAlcada.PorEquipeDeEscrevente(equipeId), regrasAtivas))
                .ToList();

            return new AlcanceDoConferente(conferente.Id, etapasPermitidas, tiposPermitidos, equipesPermitidas);
        }).ToList();
    }

    private static bool EhPermitido(Conferente conferente, AlvoAlcada alvo, IReadOnlyCollection<RegraAlcada> regras) =>
        ResolvedorAlcada.Resolver(conferente, alvo, regras).Resultado == ResultadoAlcada.Permitido;
}

public sealed record AlcanceDoConferente(
    Guid ConferenteId, IReadOnlyList<Etapa> EtapasPermitidas, IReadOnlyList<Guid> TiposPermitidosIds,
    IReadOnlyList<Guid?> EquipesPermitidasIds);
