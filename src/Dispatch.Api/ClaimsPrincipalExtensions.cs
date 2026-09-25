using System.Security.Claims;

namespace Dispatch.Api;

// Resolver Usuario.Id a partir do JWT era um Guid.Parse(FindFirstValue(...)) repetido em 7
// endpoints diferentes mais o OnTokenValidated de Program.cs (achado numa auditoria de
// qualidade) — um lugar só, reaproveitado por todos.
public static class ClaimsPrincipalExtensions
{
    public static Guid ObterUsuarioId(this ClaimsPrincipal principal) =>
        Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Perfil Administrador (ADR-0039): o endpoint passa isso pro caso de uso como flag, e é a
    // Application que corta nível/score — assim o corte é testável com fakes, sem HttpContext.
    public static bool EhAdministrador(this ClaimsPrincipal principal) =>
        principal.IsInRole(nameof(Dispatch.Domain.Papel.Administrador));
}
