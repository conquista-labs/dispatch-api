namespace Dispatch.Domain;

// Registro de auditoria, sem lógica — mesmo padrão de PedidoReabertura: a entidade já É o
// evento, sem tabela genérica "evento_decisao" (decisão já tomada antes neste projeto).
public sealed class EventoAutenticacao
{
    public Guid Id { get; }
    public Guid UsuarioId { get; }
    public TipoEventoAutenticacao Tipo { get; }
    public DateTimeOffset CriadoEm { get; }

    public EventoAutenticacao(Guid id, Guid usuarioId, TipoEventoAutenticacao tipo, DateTimeOffset criadoEm)
    {
        Id = id;
        UsuarioId = usuarioId;
        Tipo = tipo;
        CriadoEm = criadoEm;
    }
}
