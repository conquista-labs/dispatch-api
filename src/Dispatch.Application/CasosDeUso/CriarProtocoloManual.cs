using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18f: cadastro de ato que chega fora do relatório, passando pelas mesmas regras de prazo e
// alçada da importação — reaproveita DistribuirProtocolo (o mesmo fluxo do endpoint avulso).
// Segue o mesmo fluxo de continuidade da importação (ResolvedorDeContinuidade): Número
// duplicado não é bloqueio automático, só quando o(s) registro(s) existentes ainda estão "em
// uso" (proteção contra cadastro duplicado por engano — a importação não precisa disso porque
// tem a linha de corte).
public sealed class CriarProtocoloManual(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    ITipoAtoRepository tiposAto,
    DistribuirProtocolo distribuirProtocolo,
    IRelogio relogio)
{
    public async Task<ResultadoCriarProtocoloManual> ExecutarAsync(
        string numero, Guid tipoAtoId, string escreventeNome, Etapa etapa, Prioridade prioridade, string? observacao,
        // "Hora de entrada" (achado real: a importação tem esse dado vindo do relatório, mas o
        // cadastro manual sempre assumia "agora" — a distribuidora não tinha como registrar um
        // ato que chegou antes da hora em que está digitando). Nulo preserva o comportamento
        // antigo (agora). Só afeta AndamentoEm (a referência do prazo, RF-38) — AtribuirA
        // continua usando o instante real da ação (via DistribuirProtocolo/IRelogio).
        DateTimeOffset? andamentoEm = null,
        CancellationToken cancellationToken = default)
    {
        var historico = await protocolos.ObterPorNumerosAsync([numero], cancellationToken);
        if (!ResolvedorDeContinuidade.PodeRecriar(historico))
        {
            return new ResultadoCriarProtocoloManual.NumeroJaExiste();
        }

        // Mesmo cuidado do ImportarLote/endpoint avulso: não confia cegamente que o
        // TipoAtoId recebido existe — se não existir no catálogo, vira nulo (tipo
        // desconhecido, RF-09), em vez de quebrar a FK na hora de gravar.
        var tipoConhecido = (await tiposAto.ObterTodosAsync(cancellationToken)).Any(t => t.Id == tipoAtoId);

        var escrevente = await ResolvedorDeEscreventePorNome.ResolverAsync(
            escreventeNome, escreventes, adicionarSeNovo: true, cancellationToken);

        var protocolo = new Protocolo(
            Guid.NewGuid(), numero, tipoConhecido ? tipoAtoId : null, escrevente.Id, etapa, andamentoEm ?? relogio.Agora, prioridade);
        // RF-15/18f: observação é opcional já na criação — o protótipo aprovado tem esse campo
        // no mesmo modal ("o conferente vê isso no card").
        protocolo.DefinirObservacao(observacao);

        var donoDaPrimeiraConferenciaId = ResolvedorDeContinuidade.Resolver(historico, etapa);
        var resultado = await distribuirProtocolo.ExecutarAsync(protocolo, escrevente, donoDaPrimeiraConferenciaId, cancellationToken);

        return new ResultadoCriarProtocoloManual.Sucesso(protocolo.Id, resultado, protocolo.VencimentoEm);
    }
}

public abstract record ResultadoCriarProtocoloManual
{
    private ResultadoCriarProtocoloManual() { }

    public sealed record Sucesso(Guid ProtocoloId, ResultadoDistribuicao Distribuicao, DateTimeOffset? VencimentoEm) : ResultadoCriarProtocoloManual;

    public sealed record NumeroJaExiste : ResultadoCriarProtocoloManual;
}
