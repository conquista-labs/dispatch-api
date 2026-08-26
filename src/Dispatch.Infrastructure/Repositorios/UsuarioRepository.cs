using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class UsuarioRepository(DispatchDbContext dbContext) : IUsuarioRepository
{
    public async Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken) =>
        await dbContext.Usuarios.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
}
