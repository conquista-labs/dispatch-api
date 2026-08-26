using Dispatch.Application;

namespace Dispatch.Infrastructure;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTimeOffset Agora => DateTimeOffset.UtcNow;
}
