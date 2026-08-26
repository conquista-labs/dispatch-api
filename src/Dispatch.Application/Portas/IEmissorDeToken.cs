using Dispatch.Domain;

namespace Dispatch.Application;

public interface IEmissorDeToken
{
    string EmitirToken(Usuario usuario);
}
