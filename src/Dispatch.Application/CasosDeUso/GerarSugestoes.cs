using Dispatch.Domain;

namespace Dispatch.Application;

// Seção 7: "job diário" — não existe scheduler/background job no projeto ainda (decisão
// registrada em CLAUDE.md), então isso roda sob demanda via endpoint (só Distribuidora). Um
// IHostedService de verdade fica pra quando isso for pra produção.
public sealed class GerarSugestoes(
    IProtocoloRepository protocolos,
    IConferenteRepository conferentes,
    IEscreventeRepository escreventes,
    ISugestaoRepository sugestoes,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<int> ExecutarAsync(CancellationToken cancellationToken = default)
    {
        // loteImportacaoId nulo em ObterParaDistribuicaoAsync devolve todos os protocolos que
        // já existiram — mesmo reaproveitamento que ObterVisaoDistribuicao já faz.
        var todosProtocolos = await protocolos.ObterParaDistribuicaoAsync(loteImportacaoId: null, cancellationToken);
        var todosConferentes = await conferentes.ObterTodosAsync(cancellationToken);
        var todosEscreventes = await escreventes.ObterTodosAsync(cancellationToken);

        var candidatos = new List<CandidatoSugestao>();
        candidatos.AddRange(GeradorDeSugestoes.TipoDesconhecido(todosProtocolos, todosConferentes));
        candidatos.AddRange(GeradorDeSugestoes.PrazoIrreal(todosProtocolos, todosEscreventes));
        candidatos.AddRange(GeradorDeSugestoes.EscreventeOrfao(todosEscreventes, todosProtocolos));
        candidatos.AddRange(GeradorDeSugestoes.RiscoQualidade(todosProtocolos, todosConferentes));

        var agora = relogio.Agora;
        var novas = 0;

        foreach (var candidato in candidatos)
        {
            var existente = await sugestoes.ObterPorChaveAtivaAsync(candidato.Chave, cancellationToken);

            // Não achou, ou a última com essa chave foi descartada e a janela de memória já
            // passou: nasce uma proposta nova. Pendente: só atualiza ocorrências/evidência
            // (dedup). Descartada dentro da janela, ou já Aplicada: não faz nada.
            if (existente is null || (existente.Status == StatusSugestao.Descartada && existente.DescartarAte <= agora))
            {
                sugestoes.Adicionar(new Sugestao(
                    Guid.NewGuid(), candidato.Chave, candidato.Payload, candidato.Evidencia, candidato.Ocorrencias,
                    candidato.IndiceConfianca, agora));
                novas++;
            }
            else if (existente.Status == StatusSugestao.Pendente)
            {
                await sugestoes.AtualizarEvidenciaAsync(
                    existente.Id, candidato.Ocorrencias, candidato.Evidencia, candidato.IndiceConfianca, agora, cancellationToken);
            }
        }

        await unitOfWork.SalvarAsync(cancellationToken);
        return novas;
    }
}
