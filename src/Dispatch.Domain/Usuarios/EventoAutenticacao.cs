namespace Dispatch.Domain;

// Registro de auditoria, sem lógica — mesmo padrão de PedidoReabertura: a entidade já É o
// evento, sem tabela genérica "evento_decisao" (decisão já tomada antes neste projeto).
public sealed class EventoAutenticacao
{
    public Guid Id { get; }
    public Guid UsuarioId { get; }
    public TipoEventoAutenticacao Tipo { get; }
    public DateTimeOffset CriadoEm { get; }

    // RNF-16: "autor, origem e horário" — faltava a origem (achado numa auditoria de
    // fidelidade contra o protótipo reexportado). IP do request, nulo quando não dá pra saber
    // (ex.: nunca deveria acontecer via HTTP real, mas o Domain não assume que sempre vem).
    public string? Origem { get; }

    public EventoAutenticacao(Guid id, Guid usuarioId, TipoEventoAutenticacao tipo, DateTimeOffset criadoEm, string? origem = null)
    {
        Id = id;
        UsuarioId = usuarioId;
        Tipo = tipo;
        CriadoEm = criadoEm;
        Origem = origem;
    }
}
