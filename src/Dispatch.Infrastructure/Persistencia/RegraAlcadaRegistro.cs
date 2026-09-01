using Dispatch.Domain;

namespace Dispatch.Infrastructure.Persistencia;

// SujeitoAlcada e AlvoAlcada, no Domain, são hierarquias fechadas (sum types) — RegraAlcada
// é ou "por pessoa" ou "por nível", nunca os dois. O EF Core não mapeia isso de forma direta
// sem entrar em inheritance mapping (TPH), então esta classe é a forma "achatada" só pra
// persistência: guarda os dois pares de coluna, sempre um nulo e outro preenchido, e quem
// traduz de volta pro tipo rico do Domain é o RegraAlcadaRepository — não o EF Core.
//
// O lado do alvo precisou de um discriminador explícito (AlvoTipo, mesmo padrão de
// TipoSugestaoRegistro) em vez do par nulo/preenchido puro: PorTodosOsAtos não tem payload
// nenhum, e PorEquipeDeEscrevente tem um payload legitimamente nulo ("sem equipe" é alvo
// válido, RF-29a) — sem o discriminador não daria pra distinguir "esta linha é sobre equipe,
// mas sem equipe" de "esta linha não é sobre equipe".
internal sealed class RegraAlcadaRegistro
{
    public Guid Id { get; set; }
    public Guid? SujeitoConferenteId { get; set; }
    public Nivel? SujeitoNivel { get; set; }
    public AlvoTipoRegistro AlvoTipo { get; set; }
    public Etapa? AlvoEtapa { get; set; }
    public Guid? AlvoTipoAtoId { get; set; }
    public Guid? AlvoEquipeId { get; set; }
    public GrupoTipoAto? AlvoGrupoTipoAto { get; set; }
    public PermissaoRegra Permissao { get; set; }
    public OrigemRegra Origem { get; set; }
    public bool Ativa { get; set; }
}

internal enum AlvoTipoRegistro
{
    Etapa,
    TipoAto,
    Equipe,
    TodosOsAtos,
    Grupo
}
