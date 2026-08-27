using Dispatch.Domain;

namespace Dispatch.Application;

// RF-40: "aplicar" executa a mudança de verdade. Cada tipo de payload mapeia pra exatamente
// um dos quatro verbos do requisito (classifica o tipo / muda o prazo / aloca o escrevente /
// cria a regra) — reaproveitando o mesmo comportamento de Domain que as telas de gestão
// (Central de regras) já usam pra fazer a mesma coisa na mão.
public sealed class AplicarSugestao(
    ISugestaoRepository sugestoes,
    ITipoAtoRepository tiposAto,
    IEquipeRepository equipes,
    IEscreventeRepository escreventes,
    IProtocoloRepository protocolos,
    IRegraAlcadaRepository regras,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoAplicarSugestao> ExecutarAsync(Guid sugestaoId, CancellationToken cancellationToken = default)
    {
        var sugestao = await sugestoes.ObterPorIdAsync(sugestaoId, cancellationToken);
        if (sugestao is null)
        {
            return ResultadoAplicarSugestao.NaoEncontrada;
        }

        if (sugestao.Status != StatusSugestao.Pendente)
        {
            return ResultadoAplicarSugestao.NaoEstaPendente;
        }

        switch (sugestao.Payload)
        {
            case PayloadSugestao.TipoDesconhecido tipoDesconhecido:
                tiposAto.Adicionar(new TipoAto(Guid.NewGuid(), tipoDesconhecido.NomeTipo));
                break;

            case PayloadSugestao.PrazoIrreal prazoIrreal:
                var equipe = await equipes.ObterPorIdAsync(prazoIrreal.EquipeId, cancellationToken);
                if (equipe is null)
                {
                    return ResultadoAplicarSugestao.ReferenciaNaoEncontrada;
                }

                var prazoNovo = new Prazo(prazoIrreal.PrazoSugerido);
                var prazoPre = prazoIrreal.Etapa == Etapa.PreConferencia ? prazoNovo : equipe.PrazoPreConferencia;
                var prazoPos = prazoIrreal.Etapa == Etapa.PosConferencia ? prazoNovo : equipe.PrazoPosConferencia;
                equipe.DefinirPrazos(prazoPre, prazoPos);
                await RecalculoDeVencimentos.AplicarAsync(equipe, escreventes, protocolos, cancellationToken);
                break;

            case PayloadSugestao.EscreventeOrfao escreventeOrfao:
                var escrevente = await escreventes.ObterPorIdAsync(escreventeOrfao.EscreventeId, cancellationToken);
                if (escrevente is null)
                {
                    return ResultadoAplicarSugestao.ReferenciaNaoEncontrada;
                }

                escrevente.MoverParaEquipe(escreventeOrfao.EquipeSugeridaId);
                break;

            case PayloadSugestao.RiscoQualidade riscoQualidade:
                regras.Adicionar(new RegraAlcada(
                    Guid.NewGuid(),
                    new SujeitoAlcada.PorNivel(riscoQualidade.NivelRestrito),
                    PermissaoRegra.Nega,
                    new AlvoAlcada.PorTipoAto(riscoQualidade.TipoAtoId),
                    OrigemRegra.Aprendida));
                break;
        }

        await sugestoes.AplicarAsync(sugestaoId, relogio.Agora, cancellationToken);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoAplicarSugestao.Sucesso;
    }
}

public enum ResultadoAplicarSugestao
{
    Sucesso,
    NaoEncontrada,
    NaoEstaPendente,
    ReferenciaNaoEncontrada
}
