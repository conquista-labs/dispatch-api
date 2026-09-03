using Dispatch.Domain;

namespace Dispatch.Application;

// RF-24a: dentro da janela de correção (tabela `config`, seção 8) depois de concluído, o
// próprio dono troca Aprovado↔Reprovado. Fora da janela, só via reabertura (RF-24d) — não é
// guarda extra aqui, é consequência de exigir estar dentro da janela.
public sealed class CorrigirResultado(
    IProtocoloRepository protocolos, IConfiguracaoRepository configuracao, IRelogio relogio, IUnitOfWork unitOfWork)
{
    public async Task<ResultadoCorrigirResultado> ExecutarAsync(
        Guid protocoloId, Conferente conferente, CancellationToken cancellationToken = default)
    {
        var protocolo = await protocolos.ObterPorIdAsync(protocoloId, cancellationToken);
        if (protocolo is null)
        {
            return new ResultadoCorrigirResultado.NaoEncontrado();
        }

        if (protocolo.DonoId != conferente.Id)
        {
            return new ResultadoCorrigirResultado.NaoEhSeu();
        }

        if (protocolo.Status is not (StatusProtocolo.Aprovado or StatusProtocolo.Reprovado))
        {
            return new ResultadoCorrigirResultado.StatusInvalido();
        }

        var agora = relogio.Agora;
        var config = await configuracao.ObterAsync(cancellationToken);
        if (protocolo.ConcluidoEm is not { } concluidoEm || agora - concluidoEm > config.JanelaDeCorrecao)
        {
            return new ResultadoCorrigirResultado.ForaDaJanela();
        }

        protocolo.CorrigirResultado(agora);
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoCorrigirResultado.Sucesso();
    }
}

public abstract record ResultadoCorrigirResultado
{
    private ResultadoCorrigirResultado() { }

    public sealed record Sucesso : ResultadoCorrigirResultado;

    public sealed record NaoEncontrado : ResultadoCorrigirResultado;

    public sealed record NaoEhSeu : ResultadoCorrigirResultado;

    public sealed record StatusInvalido : ResultadoCorrigirResultado;

    public sealed record ForaDaJanela : ResultadoCorrigirResultado;
}
