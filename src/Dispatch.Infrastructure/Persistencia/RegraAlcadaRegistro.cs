using Dispatch.Domain;

namespace Dispatch.Infrastructure.Persistencia;

// SujeitoAlcada e AlvoAlcada, no Domain, são hierarquias fechadas (sum types) — RegraAlcada
// é ou "por pessoa" ou "por nível", nunca os dois. O EF Core não mapeia isso de forma direta
// sem entrar em inheritance mapping (TPH), então esta classe é a forma "achatada" só pra
// persistência: guarda os dois pares de coluna, sempre um nulo e outro preenchido, e quem
// traduz de volta pro tipo rico do Domain é o RegraAlcadaRepository — não o EF Core.
internal sealed class RegraAlcadaRegistro
{
    public Guid Id { get; set; }
    public Guid? SujeitoConferenteId { get; set; }
    public Nivel? SujeitoNivel { get; set; }
    public Etapa? AlvoEtapa { get; set; }
    public Guid? AlvoTipoAtoId { get; set; }
    public PermissaoRegra Permissao { get; set; }
    public OrigemRegra Origem { get; set; }
    public bool Ativa { get; set; }
}
