using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class ConferenteConfiguration : IEntityTypeConfiguration<Conferente>
{
    public void Configure(EntityTypeBuilder<Conferente> builder)
    {
        builder.ToTable("conferentes");
        builder.HasKey(c => c.Id);

        // Enum guardado como texto ("Junior", não 0) — sobrevive a reordenar valores do
        // enum no C# sem corromper dado já gravado, e dá pra ler direto no DBeaver.
        builder.Property(c => c.Nivel).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.JornadaHoras);
        builder.Property(c => c.NaEscala);
        builder.Property(c => c.CargaAtual);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(c => c.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => c.UsuarioId).IsUnique();
    }
}
