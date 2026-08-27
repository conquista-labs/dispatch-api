namespace Dispatch.Application;

// RF-35 (mover escrevente) + RF-37 (permitir alocação de quem está sem equipe — equipeId
// não-nulo resolve os dois casos com o mesmo código; equipeId nulo tira da equipe).
public sealed class MoverEscreventeParaEquipe(
    IEscreventeRepository escreventes,
    IEquipeRepository equipes,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoMoverEscrevente> ExecutarAsync(
        Guid escreventeId, Guid? equipeId, CancellationToken cancellationToken = default)
    {
        var escrevente = await escreventes.ObterPorIdAsync(escreventeId, cancellationToken);
        if (escrevente is null)
        {
            return ResultadoMoverEscrevente.EscreventeNaoEncontrado;
        }

        if (equipeId is { } idInformado)
        {
            var equipe = await equipes.ObterPorIdAsync(idInformado, cancellationToken);
            if (equipe is null)
            {
                return ResultadoMoverEscrevente.EquipeNaoEncontrada;
            }
        }

        escrevente.MoverParaEquipe(equipeId);
        await unitOfWork.SalvarAsync(cancellationToken);
        return ResultadoMoverEscrevente.Sucesso;
    }
}

public enum ResultadoMoverEscrevente
{
    Sucesso,
    EscreventeNaoEncontrado,
    EquipeNaoEncontrada
}
