namespace Dispatch.Domain;

// RF-24b/c: fora da janela de correção (RF-24a), o conferente não devolve o ato pra
// conferência sozinho — abre um pedido, que a distribuidora decide (aprova = reabre o
// protocolo, ou nega). Mapeamento direto (como Protocolo/TipoAto), sem Registro achatado —
// não é um sum type, não precisa disso.
public sealed class PedidoReabertura
{
    public Guid Id { get; }
    public Guid ProtocoloId { get; }
    public Guid SolicitanteId { get; }
    public DateTimeOffset CriadoEm { get; }
    public StatusPedidoReabertura Status { get; private set; }
    public Guid? DecididoPorId { get; private set; }
    public DateTimeOffset? DecididoEm { get; private set; }

    public PedidoReabertura(Guid id, Guid protocoloId, Guid solicitanteId, DateTimeOffset criadoEm)
    {
        Id = id;
        ProtocoloId = protocoloId;
        SolicitanteId = solicitanteId;
        CriadoEm = criadoEm;
        Status = StatusPedidoReabertura.Pendente;
    }

    public void Aprovar(Guid decididoPorId, DateTimeOffset agora)
    {
        Status = StatusPedidoReabertura.Aprovado;
        DecididoPorId = decididoPorId;
        DecididoEm = agora;
    }

    public void Negar(Guid decididoPorId, DateTimeOffset agora)
    {
        Status = StatusPedidoReabertura.Negado;
        DecididoPorId = decididoPorId;
        DecididoEm = agora;
    }

    // RF-24b: cancelável enquanto pendente — só o próprio solicitante (checado no caso de uso).
    public void Cancelar() => Status = StatusPedidoReabertura.Cancelado;
}
