using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24c: alimenta a seção "Pedidos de reabertura" da aba de Exceções — junta o pedido com o
// protocolo (número/tipo/etapa) e o nome de quem pediu, mesmo padrão de ListarConferentes
// (Conferente não guarda nome, é dado de Usuario).
public sealed class ListarPedidosReaberturaPendentes(
    IPedidoReaberturaRepository pedidos,
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IUsuarioRepository usuarios)
{
    public async Task<IReadOnlyList<PedidoReaberturaResumo>> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        var pendentes = await pedidos.ObterPendentesAsync(cancellationToken);
        if (pendentes.Count == 0)
        {
            return [];
        }

        var todosConferentes = await conferentes.ObterTodosAsync(cancellationToken);
        var conferentePorId = todosConferentes.ToDictionary(c => c.Id);
        var usuarioIds = pendentes
            .Select(p => conferentePorId.GetValueOrDefault(p.SolicitanteId))
            .Where(c => c is not null)
            .Select(c => c!.UsuarioId)
            .ToList();
        var usuarioPorId = (await usuarios.ObterVariosPorIdsAsync(usuarioIds, cancellationToken)).ToDictionary(u => u.Id);

        var resumos = new List<PedidoReaberturaResumo>();
        foreach (var pedido in pendentes)
        {
            var protocolo = await protocolos.ObterPorIdAsync(pedido.ProtocoloId, cancellationToken);
            if (protocolo is null)
            {
                continue;
            }

            var nomeSolicitante = conferentePorId.TryGetValue(pedido.SolicitanteId, out var conferente)
                && usuarioPorId.TryGetValue(conferente.UsuarioId, out var usuario)
                ? usuario.Nome
                : "—";

            resumos.Add(new PedidoReaberturaResumo(
                pedido.Id, pedido.ProtocoloId, protocolo.Numero, protocolo.TipoAtoId, protocolo.Etapa,
                protocolo.Status, pedido.SolicitanteId, nomeSolicitante, pedido.CriadoEm));
        }

        return resumos;
    }
}

public sealed record PedidoReaberturaResumo(
    Guid PedidoId,
    Guid ProtocoloId,
    string ProtocoloNumero,
    Guid? TipoAtoId,
    Etapa Etapa,
    StatusProtocolo StatusAtual,
    Guid SolicitanteId,
    string NomeSolicitante,
    DateTimeOffset CriadoEm);
