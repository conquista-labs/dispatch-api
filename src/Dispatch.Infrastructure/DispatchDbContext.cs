using Dispatch.Domain;
using Dispatch.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
    public DbSet<UsuarioTotp> UsuariosTotp => Set<UsuarioTotp>();
    public DbSet<EventoAutenticacao> EventosAutenticacao => Set<EventoAutenticacao>();
    public DbSet<LoteImportacao> LotesImportacao => Set<LoteImportacao>();
    public DbSet<PedidoReabertura> PedidosReabertura => Set<PedidoReabertura>();
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    internal DbSet<RegraAlcadaRegistro> RegrasDeAlcada => Set<RegraAlcadaRegistro>();
    internal DbSet<SugestaoRegistro> Sugestoes => Set<SugestaoRegistro>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DispatchDbContext).Assembly);
    }

    // Npgsql recusa gravar DateTimeOffset com offset != 0 em `timestamp with time zone`
    // ("only offset 0 (UTC) is supported") — a coluna guarda o instante em UTC de qualquer
    // forma, o offset nunca é persistido. Sem esta normalização, um cliente que mande
    // "2026-03-10T09:00:00-03:00" (ISO-8601 perfeitamente válido, o horário de Brasília que
    // qualquer cliente fora do navegador escreveria) derruba a gravação inteira com 500.
    // O dispatch-web nunca esbarrou nisso porque Date.toISOString() do JS sempre emite UTC —
    // era um 500 latente, achado pelo primeiro teste de integração contra Postgres de verdade.
    // Converter na escrita preserva o instante exato, só troca a representação.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetParaUtcConverter>();
    }
}

internal sealed class DateTimeOffsetParaUtcConverter()
    : ValueConverter<DateTimeOffset, DateTimeOffset>(valor => valor.ToUniversalTime(), valor => valor);
