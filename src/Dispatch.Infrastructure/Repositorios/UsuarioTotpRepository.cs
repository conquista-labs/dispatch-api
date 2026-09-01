using Dispatch.Application;
using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure.Repositorios;

public sealed class UsuarioTotpRepository(DispatchDbContext dbContext) : IUsuarioTotpRepository
{
    public async Task<UsuarioTotp?> ObterPorUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        await dbContext.UsuariosTotp.SingleOrDefaultAsync(u => u.UsuarioId == usuarioId, cancellationToken);

    public void Adicionar(UsuarioTotp usuarioTotp) => dbContext.UsuariosTotp.Add(usuarioTotp);
}
