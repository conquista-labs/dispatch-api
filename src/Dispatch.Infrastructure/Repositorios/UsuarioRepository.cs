using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class UsuarioRepository(DispatchDbContext dbContext) : IUsuarioRepository
{
    public async Task<Usuario?> ObterPorEmailAsync(string email, CancellationToken cancellationToken) =>
        await dbContext.Usuarios.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

    public async Task<Usuario?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Usuarios.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyCollection<Usuario>> ObterVariosPorIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await dbContext.Usuarios.Where(u => ids.Contains(u.Id)).ToListAsync(cancellationToken);

    public async Task<bool> ExisteComEmailAsync(string email, CancellationToken cancellationToken) =>
        await dbContext.Usuarios.AnyAsync(u => u.Email == email, cancellationToken);

    public void Adicionar(Usuario usuario) => dbContext.Usuarios.Add(usuario);
}
