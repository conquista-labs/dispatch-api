using Dispatch.Domain;

namespace Dispatch.Application;

public abstract record ResultadoAutenticacao
{
    private ResultadoAutenticacao() { }

    // Dados do usuário vão junto do token — evita o front ter que decodificar o JWT (ou fazer
    // uma segunda chamada) só pra saber quem acabou de logar. Papeis (não Papel) — ver
    // PapeisEfetivos: alguém pode ter mais de um (distribuidora que também confere).
    // TrocarSenha (RF-45, ADR-0040): conta com senha inicial — o front manda pra tela de troca, e
    // o token só vale pra isso até trocar.
    public sealed record Autenticado(
        string Token, Guid UsuarioId, string Nome, string Email, IReadOnlyList<Papel> Papeis, bool TrocarSenha = false) : ResultadoAutenticacao;

    // Mesmo resultado pra e-mail inexistente, senha errada ou usuário inativo — não dá
    // pista de qual dos três foi, pra não facilitar enumeração de e-mails cadastrados.
    public sealed record Rejeitado : ResultadoAutenticacao;
}
