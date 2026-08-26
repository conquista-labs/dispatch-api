namespace Dispatch.Application;

public abstract record ResultadoAutenticacao
{
    private ResultadoAutenticacao() { }

    public sealed record Autenticado(string Token) : ResultadoAutenticacao;

    // Mesmo resultado pra e-mail inexistente, senha errada ou usuário inativo — não dá
    // pista de qual dos três foi, pra não facilitar enumeração de e-mails cadastrados.
    public sealed record Rejeitado : ResultadoAutenticacao;
}
