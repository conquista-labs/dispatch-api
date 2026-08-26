namespace Dispatch.Application;

public abstract record ResultadoCadastroConferente
{
    private ResultadoCadastroConferente() { }

    public sealed record Sucesso(Guid ConferenteId) : ResultadoCadastroConferente;

    public sealed record EmailJaCadastrado : ResultadoCadastroConferente;
}
