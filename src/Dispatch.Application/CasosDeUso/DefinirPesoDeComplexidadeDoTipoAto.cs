using Dispatch.Domain;

namespace Dispatch.Application;

// RF-34f: alimenta a parcela de complexidade do score (RF-46) e a estimativa do tempo de referência
// (RF-46c). Decimal 0,50–2,50 em passos de 0,05 (PesoDeComplexidade) — fora disso é 400 com motivo.
// Até a fatia 5 do Dashboard v2 era inteiro e clampado pra ≥ 1 em silêncio; clamp não serve mais:
// "1,33" arredondado sem aviso gravaria um peso que o administrador não escolheu.
public sealed class DefinirPesoDeComplexidadeDoTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoDefinirPesoDeComplexidade> ExecutarAsync(Guid tipoAtoId, decimal peso, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return new ResultadoDefinirPesoDeComplexidade.NaoEncontrado();
        }

        if (PesoDeComplexidade.Validar(peso) is { } motivo)
        {
            return new ResultadoDefinirPesoDeComplexidade.Invalido(motivo);
        }

        tipoAto.DefinirPesoDeComplexidade(peso);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoDefinirPesoDeComplexidade.Sucesso();
    }
}

public abstract record ResultadoDefinirPesoDeComplexidade
{
    private ResultadoDefinirPesoDeComplexidade() { }

    public sealed record Sucesso : ResultadoDefinirPesoDeComplexidade;

    public sealed record NaoEncontrado : ResultadoDefinirPesoDeComplexidade;

    public sealed record Invalido(string Motivo) : ResultadoDefinirPesoDeComplexidade;
}
