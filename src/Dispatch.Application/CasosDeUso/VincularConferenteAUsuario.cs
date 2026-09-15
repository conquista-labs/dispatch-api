using Dispatch.Domain;

namespace Dispatch.Application;

// Pedido do dono: uma distribuidora que também confere pessoalmente (a mesma conta, os dois
// papéis) — diferente de CadastrarConferente (RF-25), que sempre cria um Usuario novo. Aqui o
// Usuario já existe (busca por e-mail — sem GET /usuarios hoje, e não precisa existir só pra
// isso, um cartório tem poucas contas de distribuidora); só ganha um Conferente vinculado.
// "Já é conferente" cobre sozinho o caso de tentar vincular alguém que já é Papel.Conferente
// (que já tem Conferente criado junto no cadastro normal) — não precisa de checagem de papel
// à parte. Ver PapeisEfetivos: o papel efetivo dessa pessoa passa a incluir Conferente a
// partir do próximo login (o JWT atual, se ela já estiver logada, não é revogado — mesmo
// comportamento de qualquer mudança de permissão, precisa logar de novo pra valer).
public sealed class VincularConferenteAUsuario(
    IUsuarioRepository usuarios,
    IConferenteRepository conferentes,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoVincularConferente> ExecutarAsync(
        string email, Nivel nivel, double jornadaHoras, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.ObterPorEmailAsync(email, cancellationToken);
        if (usuario is null)
        {
            return new ResultadoVincularConferente.UsuarioNaoEncontrado();
        }

        var existente = await conferentes.ObterPorUsuarioIdAsync(usuario.Id, cancellationToken);
        if (existente is not null)
        {
            return new ResultadoVincularConferente.JaEhConferente();
        }

        var conferente = new Conferente(Guid.NewGuid(), usuario.Id, nivel, jornadaHoras, naEscala: true, cargaAtual: 0);
        conferentes.Adicionar(conferente);
        await unitOfWork.SalvarAsync(cancellationToken);

        return new ResultadoVincularConferente.Sucesso(conferente.Id);
    }
}

public abstract record ResultadoVincularConferente
{
    private ResultadoVincularConferente() { }

    public sealed record Sucesso(Guid ConferenteId) : ResultadoVincularConferente;

    public sealed record UsuarioNaoEncontrado : ResultadoVincularConferente;

    public sealed record JaEhConferente : ResultadoVincularConferente;
}
