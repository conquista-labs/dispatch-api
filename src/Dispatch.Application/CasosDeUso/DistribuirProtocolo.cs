using Dispatch.Domain;

namespace Dispatch.Application;

// Orquestra o que já existe em Dispatch.Domain: resolve o prazo do escrevente (via
// ResolvedorDePrazo), roda o motor de distribuição (via MotorDistribuicao) e aplica o
// resultado no Protocolo antes de gravar. Nenhuma regra de negócio nova aqui — só a
// sequência de passos, a leitura dos dados que o Domain precisa e a persistência do efeito.
public sealed class DistribuirProtocolo(
    IConferenteRepository conferentes,
    IEquipeRepository equipes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
    IProtocoloRepository protocolos,
    IUnitOfWork unitOfWork,
    IRelogio relogio)
{
    public async Task<ResultadoDistribuicao> ExecutarAsync(
        Protocolo protocolo,
        Escrevente escrevente,
        CancellationToken cancellationToken = default)
    {
        var resolucaoPrazo = ResolvedorDePrazo.Resolver(
            escrevente,
            protocolo.Etapa,
            await equipes.ObterTodasAsync(cancellationToken));

        protocolo.DefinirPrazo(resolucaoPrazo.Prazo, relogio.Agora);

        var resultado = MotorDistribuicao.Distribuir(
            protocolo,
            await conferentes.ObterNaEscalaAsync(cancellationToken),
            await regras.ObterAtivasAsync(cancellationToken),
            await tiposAto.ObterTodosAsync(cancellationToken));

        switch (resultado)
        {
            case ResultadoDistribuicao.Atribuido atribuido:
                protocolo.AtribuirA(atribuido.Conferente.Id);
                break;
            case ResultadoDistribuicao.EnviadoParaPool:
                protocolo.EnviarParaPool();
                break;
            case ResultadoDistribuicao.Excecao excecao:
                protocolo.MarcarExcecao(excecao.Motivo);
                break;
        }

        protocolos.Adicionar(protocolo);
        await unitOfWork.SalvarAsync(cancellationToken);

        return resultado;
    }
}
