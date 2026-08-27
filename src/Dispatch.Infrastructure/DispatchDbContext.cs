using Dispatch.Domain;
using Dispatch.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Dispatch.Infrastructure;

// Equivalente ao PrismaClient: a "porta de entrada" pro banco. Cada DbSet é uma tabela
// consultável (dbContext.Conferentes.Where(...) ~ prisma.conferente.findMany({ where: ... })).
public sealed class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
{
    public DbSet<TipoAto> TiposAto => Set<TipoAto>();
    public DbSet<Conferente> Conferentes => Set<Conferente>();
    public DbSet<Equipe> Equipes => Set<Equipe>();
    public DbSet<Escrevente> Escreventes => Set<Escrevente>();
    public DbSet<Protocolo> Protocolos => Set<Protocolo>();
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<LoteImportacao> LotesImportacao => Set<LoteImportacao>();
    internal DbSet<RegraAlcadaRegistro> RegrasDeAlcada => Set<RegraAlcadaRegistro>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchDbContext).Assembly);
    }
}
