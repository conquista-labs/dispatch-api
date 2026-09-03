using System.Security.Claims;

namespace Dispatch.Api;

// Resolver Usuario.Id a partir do JWT era um Guid.Parse(FindFirstValue(...)) repetido em 7
// endpoints diferentes mais o OnTokenValidated de Program.cs (achado numa auditoria de
// qualidade) — um lugar só, reaproveitado por todos.
public static class ClaimsPrincipalExtensions
{
    public static Guid ObterUsuarioId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
