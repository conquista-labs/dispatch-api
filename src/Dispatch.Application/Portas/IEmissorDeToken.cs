using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEmissorDeToken
{
    // "papeis" é o papel efetivo (ver PapeisEfetivos) — pode ter mais de um quando o usuário
    // também tem um Conferente vinculado, além do próprio Usuario.Papel.
    string EmitirToken(Usuario usuario, IReadOnlyCollection<Papel> papeis);
}
