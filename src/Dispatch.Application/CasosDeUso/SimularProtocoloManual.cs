using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18f: "o modal mostra, antes de confirmar, a equipe, o prazo, o grupo do ato e o destino
// previsto (dono, pool ou exceção)". Roda a mesma sequência de AplicadorDeDistribuicao sobre um
// Protocolo transitório (nunca chega a existir no banco) e um Escrevente resolvido só em
// memória (adicionarSeNovo: false) — nada é persistido aqui, mesmo se o escrevente digitado
// ainda não existir no catálogo.
public sealed class SimularProtocoloManual(
    IConferenteRepository conferentes,
    IEquipeRepository equipes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IEscreventeRepository escreventes,
    IProtocoloRepository protocolos,
    IRelogio relogio)
{
    public async Task<ResultadoSimulacaoProtocolo> ExecutarAsync(
        string numero, Guid tipoAtoId, string escreventeNome, Etapa etapa, Prioridade prioridade,
        CancellationToken cancellationToken = default)
    {
        var numeroDisponivel = !await protocolos.ExisteComNumeroAsync(numero, cancellationToken);
        var catalogoTipos = await tiposAto.ObterTodosAsync(cancellationToken);
        var tipoAto = catalogoTipos.FirstOrDefault(t => t.Id == tipoAtoId);

        var escrevente = await ResolvedorDeEscreventePorNome.ResolverAsync(
            escreventeNome, escreventes, adicionarSeNovo: false, cancellationToken);

        var agora = relogio.Agora;
        var protocolo = new Protocolo(Guid.NewGuid(), numero, tipoAto?.Id, escrevente.Id, etapa, agora, prioridade);

        var resultado = AplicadorDeDistribuicao.Executar(
            protocolo,
            escrevente,
            await equipes.ObterTodasAsync(cancellationToken),
            await conferentes.ObterNaEscalaAsync(cancellationToken),
            await regras.ObterAtivasAsync(cancellationToken),
            catalogoTipos,
            agora,
            out var resolucaoPrazo);

        var (destino, conferenteId, motivo) = resultado switch
        {
            ResultadoDistribuicao.Atribuido atribuido => ("Atribuido", (Guid?)atribuido.Conferente.Id, (string?)null),
            ResultadoDistribuicao.EnviadoParaPool => ("EnviadoParaPool", null, null),
            ResultadoDistribuicao.Excecao excecao => ("Excecao", null, excecao.Motivo),
            _ => throw new InvalidOperationException($"Resultado de distribuição não mapeado: {resultado.GetType().Name}")
        };

        return new ResultadoSimulacaoProtocolo(
            numeroDisponivel, tipoAto?.Grupo, resolucaoPrazo.Equipe?.Nome, resolucaoPrazo.SemEquipeSinalizado,
            resolucaoPrazo.Prazo.Tipo, protocolo.VencimentoEm!.Value, destino, conferenteId, motivo);
    }
}

public sealed record ResultadoSimulacaoProtocolo(
    bool NumeroDisponivel,
    GrupoTipoAto? Grupo,
    string? EquipeNome,
    bool SemEquipeSinalizado,
    TipoPrazo Prazo,
    DateTimeOffset VencimentoEm,
    string Destino,
    Guid? ConferenteId,
    string? Motivo);
