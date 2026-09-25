using Dispatch.Domain;

namespace Dispatch.Application;

// RF-34a: o stepper de minutos grava o valor informado (2–240); nulo é o "usar histórico" — apaga o
// informado e a referência efetiva volta pra mediana (ou estimativa, se o tipo ainda não tem 30
// conferências). A referência efetiva não é gravada: é calculada na leitura (TempoDeReferencia).
public sealed class DefinirTempoDeReferenciaDoTipoAto(ITipoAtoRepository tiposAto, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoDefinirTempoDeReferencia> ExecutarAsync(Guid tipoAtoId, int? minutos, CancellationToken cancellationToken = default)
    {
        var tipoAto = await tiposAto.ObterPorIdAsync(tipoAtoId, cancellationToken);
        if (tipoAto is null)
        {
            return new ResultadoDefinirTempoDeReferencia.NaoEncontrado();
        }

        if (TempoDeReferencia.ValidarInformado(minutos) is { } motivo)
        {
            return new ResultadoDefinirTempoDeReferencia.Invalido(motivo);
        }

        tipoAto.DefinirTempoDeReferencia(minutos);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoDefinirTempoDeReferencia.Sucesso();
    }
}

public abstract record ResultadoDefinirTempoDeReferencia
{
    private ResultadoDefinirTempoDeReferencia() { }

    public sealed record Sucesso : ResultadoDefinirTempoDeReferencia;

    public sealed record NaoEncontrado : ResultadoDefinirTempoDeReferencia;

    public sealed record Invalido(string Motivo) : ResultadoDefinirTempoDeReferencia;
}
