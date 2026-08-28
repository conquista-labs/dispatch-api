namespace Dispatch.Application;

// RF-25 "editar" — nome/e-mail, separado de EditarNivelEJornada (RF-26) de propósito: são
// campos do Usuario, não do Conferente, e o front trata isso como uma ação separada (editar
// perfil vs. os controles rápidos de nível/jornada direto no card).
public sealed class EditarPerfilConferente(
    IConferenteRepository conferentes,
    IUsuarioRepository usuarios,
    IUnitOfWork unitOfWork)
{
    public async Task<ResultadoEditarPerfilConferente> ExecutarAsync(
        Guid conferenteId, string nome, string email, CancellationToken cancellationToken = default)
    {
        var conferente = await conferentes.ObterPorIdAsync(conferenteId, cancellationToken);
        if (conferente is null)
        {
            return new ResultadoEditarPerfilConferente.NaoEncontrado();
        }

        var usuario = await usuarios.ObterPorIdAsync(conferente.UsuarioId, cancellationToken);
        if (usuario is null)
        {
            return new ResultadoEditarPerfilConferente.NaoEncontrado();
        }

        var emailMudou = !string.Equals(usuario.Email, email, StringComparison.OrdinalIgnoreCase);
        if (emailMudou && await usuarios.ExisteComEmailAsync(email, cancellationToken))
        {
            return new ResultadoEditarPerfilConferente.EmailJaCadastrado();
        }

        usuario.AtualizarPerfil(nome, email);
        await unitOfWork.SalvarAsync(cancellationToken);

        return new ResultadoEditarPerfilConferente.Sucesso();
    }
}

public abstract record ResultadoEditarPerfilConferente
{
    private ResultadoEditarPerfilConferente() { }

    public sealed record Sucesso : ResultadoEditarPerfilConferente;

    public sealed record NaoEncontrado : ResultadoEditarPerfilConferente;

    public sealed record EmailJaCadastrado : ResultadoEditarPerfilConferente;
}
