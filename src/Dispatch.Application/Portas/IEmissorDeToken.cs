using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEmissorDeToken
{
    // "papeis" é o papel efetivo (ver PapeisEfetivos) — pode ter mais de um quando o usuário
    // também tem um Conferente vinculado, além do próprio Usuario.Papel.
    // Com Usuario.TrocarSenhaNoProximoAcesso ligado, o token leva a claim ClaimsDoDispatch.TrocarSenha,
    // e a Api só aceita esse token pra trocar a senha (ADR-0040).
    string EmitirToken(Usuario usuario, IReadOnlyCollection<Papel> papeis);
}

public static class ClaimsDoDispatch
{
    public const string TrocarSenha = "trocar_senha";
}
