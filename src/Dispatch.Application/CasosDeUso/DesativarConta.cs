using Dispatch.Domain;

namespace Dispatch.Application;

// RF-46/RF-47: desativar tira o acesso e preserva histórico e regras criadas (soft delete, igual
// RemoverConferente). Travas, cada uma com um desfecho próprio pro front mostrar o motivo certo:
// ninguém desativa a própria conta; sempre resta pelo menos um administrador ativo; conta de
// Conferente puro se remove em Conferentes, não aqui. Conta que também confere passa pelos mesmos
// efeitos de RemoverConferente (sai da escala, os atribuídos voltam ao pool).
public sealed class DesativarConta(
    IUsuarioRepository usuarios,
    IConferenteRepository conferentes,
    RemoverConferente removerConferente,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoDesativarConta> ExecutarAsync(
        Guid contaId, Guid usuarioLogadoId, CancellationToken cancellationToken = default)
    {
        var conta = await usuarios.ObterPorIdAsync(contaId, cancellationToken);
        if (conta is null)
        {
            return new ResultadoDesativarConta.NaoEncontrada();
        }

        if (conta.Papel == Papel.Conferente)
        {
            return new ResultadoDesativarConta.ContaDeConferente();
        }

        if (!conta.Ativo)
        {
            return new ResultadoDesativarConta.JaInativa();
        }

        var ehUltimoAdmin = conta.Papel == Papel.Administrador
            && (await usuarios.ObterPorPapeisAsync([Papel.Administrador], cancellationToken)).Count(u => u.Ativo) <= 1;
        var ehAPropria = conta.Id == usuarioLogadoId;

        if (ehAPropria && ehUltimoAdmin)
        {
            return new ResultadoDesativarConta.PropriaEUltimoAdministrador();
        }

        if (ehAPropria)
        {
            return new ResultadoDesativarConta.PropriaConta();
        }

        if (ehUltimoAdmin)
        {
            return new ResultadoDesativarConta.UltimoAdministrador();
        }

        if (await conferentes.ObterPorUsuarioIdAsync(conta.Id, cancellationToken) is { } conferente)
        {
            // Desativa o Usuario, tira da escala e devolve os atribuídos ao pool, num SalvarAsync só.
            await removerConferente.ExecutarAsync(conferente.Id, cancellationToken);
            return new ResultadoDesativarConta.Sucesso();
        }

        conta.Desativar();
        await unitOfWork.SalvarAsync(cancellationToken);
        return new ResultadoDesativarConta.Sucesso();
    }
}

public abstract record ResultadoDesativarConta
{
    private ResultadoDesativarConta() { }

    public sealed record Sucesso : ResultadoDesativarConta;

    public sealed record NaoEncontrada : ResultadoDesativarConta;

    public sealed record ContaDeConferente : ResultadoDesativarConta;

    public sealed record JaInativa : ResultadoDesativarConta;

    public sealed record PropriaConta : ResultadoDesativarConta;

    public sealed record UltimoAdministrador : ResultadoDesativarConta;

    public sealed record PropriaEUltimoAdministrador : ResultadoDesativarConta;
}
