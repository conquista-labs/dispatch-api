using Dispatch.Domain;

namespace Dispatch.Application;

// RF-18g: trocar tipo/escrevente/etapa recalcula equipe, prazo e vencimento (a partir do
// AndamentoEm original, nunca de "agora" — mesma regra de RecalculoDeVencimentos/RF-38);
// mudar só prioridade ou observação não mexe no vencimento. RF-18h: se o dono atual perder a
// alçada por causa da mudança, volta pro pool sozinho.
public sealed class EditarProtocoloManual(
    IProtocoloRepository protocolos,
    IEscreventeRepository escreventes,
    IEquipeRepository equipes,
    IConferenteRepository conferentes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoEditarProtocoloManual> ExecutarAsync(
        Guid id, Guid tipoAtoId, string escreventeNome, Etapa etapa, Prioridade prioridade, string? observacao,
        CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(id, cancellationToken);
        if (protocolo is null)
        {
            return new ResultadoEditarProtocoloManual.NaoEncontrado();
        }

        // Mesmo cuidado do endpoint avulso: TipoAtoId que não existe mais no catálogo vira
        // nulo (tipo desconhecido), não quebra a FK na hora de salvar.
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        var escrevente = await ResolvedorDeEscreventePorNome.ResolverAsync(
            escreventeNome, escreventes, adicionarSeNovo: true, cancellationToken);

        var identidadeMudou = tipoAto?.Id != protocolo.TipoAtoId || escrevente.Id != protocolo.EscreventeId || etapa != protocolo.Etapa;

        protocolo.EditarDadosBasicos(tipoAto?.Id, escrevente.Id, etapa);
        protocolo.DefinirPrioridade(prioridade);
        protocolo.DefinirObservacao(observacao);

        if (identidadeMudou)
        {
            var todasEquipes = await equipes.ObterTodasAsync(cancellationToken);
            var resolucaoPrazo = ResolvedorDePrazo.Resolver(escrevente, etapa, todasEquipes);
            protocolo.DefinirPrazo(resolucaoPrazo.Prazo, protocolo.AndamentoEm);

            // RF-18h: só faz sentido checar quem já é dono (Atribuido/Conferindo) — pool/
            // exceção/concluído/excluído não tem "dono perdendo alçada" pra falar. Sem tipo
            // conhecido também não dá pra checar alçada nenhuma (o motor trataria isso como
            // exceção de "tipo desconhecido", que este fluxo de edição não reabre sozinho).
            if (protocolo.DonoId is { } donoId && tipoAto is not null)
            {
                var dono = await conferentes.ObterPorIdAsync(donoId, cancellationToken);
                var regrasAtivas = await regras.ObterAtivasAsync(cancellationToken);
                if (dono is not null && !VerificadorDeAlcada.TemAlcada(dono, protocolo, tipoAto, escrevente.EquipeId, regrasAtivas))
                {
                    protocolo.EnviarParaPool();
                }
            }
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoEditarProtocoloManual.Sucesso();
    }
}

public abstract record ResultadoEditarProtocoloManual
{
    private ResultadoEditarProtocoloManual() { }

    public sealed record Sucesso : ResultadoEditarProtocoloManual;

    public sealed record NaoEncontrado : ResultadoEditarProtocoloManual;
}
