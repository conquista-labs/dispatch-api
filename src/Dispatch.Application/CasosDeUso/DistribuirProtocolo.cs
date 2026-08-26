using Dispatch.Domain;

namespace Dispatch.Application;

// Orquestra o que já existe em Dispatch.Domain: resolve o prazo do escrevente (via
// ResolvedorDePrazo) e então roda o motor de distribuição (via MotorDistribuicao). Nenhuma
// regra de negócio nova aqui — só a sequência de passos e a leitura dos dados que o Domain
// precisa, vindos das portas.
public sealed class DistribuirProtocolo(
    IConferenteRepository conferentes,
    IEquipeRepository equipes,
    IRegraAlcadaRepository regras,
    ITipoAtoRepository tiposAto,
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

        return MotorDistribuicao.Distribuir(
            protocolo,
            await conferentes.ObterNaEscalaAsync(cancellationToken),
            await regras.ObterAtivasAsync(cancellationToken),
            await tiposAto.ObterTodosAsync(cancellationToken));
    }
}
