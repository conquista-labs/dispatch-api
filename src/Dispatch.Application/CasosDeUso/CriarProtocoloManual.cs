using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18f: cadastro de ato que chega fora do relatório, passando pelas mesmas regras de prazo e
// alçada da importação — reaproveita DistribuirProtocolo (o mesmo fluxo do endpoint avulso), só
// acrescentando o bloqueio de número duplicado que o cadastro manual pede (diferente da
// importação, que tolera Numero repetido de propósito — ver ImportarLote/"linha de corte").
public sealed class CriarProtocoloManual(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    ITipoAtoRepository tiposAto,
    DistribuirProtocolo distribuirProtocolo,
    IRelogio relogio)
{
    public async Task<ResultadoCriarProtocoloManual> ExecutarAsync(
        string numero, Guid tipoAtoId, string escreventeNome, Etapa etapa, Prioridade prioridade, string? observacao,
        CancellationToken cancellationToken = default)
    {
        if (await protocolos.ExisteComNumeroAsync(numero, cancellationToken))
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
            Guid.NewGuid(), numero, tipoConhecido ? tipoAtoId : null, escrevente.Id, etapa, relogio.Agora, prioridade);
        // RF-15/18f: observação é opcional já na criação — o protótipo aprovado tem esse campo
        // no mesmo modal ("o conferente vê isso no card").
        protocolo.DefinirObservacao(observacao);
        var resultado = await distribuirProtocolo.ExecutarAsync(protocolo, escrevente, cancellationToken);

        return new ResultadoCriarProtocoloManual.Sucesso(protocolo.Id, resultado, protocolo.VencimentoEm);
    }
}

public abstract record ResultadoCriarProtocoloManual
{
    private ResultadoCriarProtocoloManual() { }

    public sealed record Sucesso(Guid ProtocoloId, ResultadoDistribuicao Distribuicao, DateTimeOffset? VencimentoEm) : ResultadoCriarProtocoloManual;

    public sealed record NumeroJaExiste : ResultadoCriarProtocoloManual;
}
