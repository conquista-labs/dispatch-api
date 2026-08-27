using Dispatch.Domain;

namespace Dispatch.Application;

public abstract record ResultadoAutenticacao
{
    private ResultadoAutenticacao() { }

    // Dados do usuário vão junto do token — evita o front ter que decodificar o JWT (ou fazer
    // uma segunda chamada) só pra saber quem acabou de logar.
    public sealed record Autenticado(string Token, Guid UsuarioId, string Nome, string Email, Papel Papel) : ResultadoAutenticacao;

    // Mesmo resultado pra e-mail inexistente, senha errada ou usuário inativo — não dá
    // pista de qual dos três foi, pra não facilitar enumeração de e-mails cadastrados.
    public sealed record Rejeitado : ResultadoAutenticacao;
}
