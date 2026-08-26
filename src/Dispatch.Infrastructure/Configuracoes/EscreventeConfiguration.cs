using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class EscreventeConfiguration : IEntityTypeConfiguration<Escrevente>
{
    public void Configure(EntityTypeBuilder<Escrevente> builder)
    {
        builder.ToTable("escreventes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Nome).IsRequired().HasMaxLength(200);

        // Escrevente não tem "public Equipe Equipe { get; }" no Domain (RF não pede navegar
        // objeto, só o id) — mas ainda dá pra declarar a foreign key sem navigation property.
        // Escrevente sem equipe (EquipeId nulo) é um caso válido de negócio (RF-09), não erro.
        builder.HasOne<Equipe>()
            .WithMany()
            .HasForeignKey(e => e.EquipeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
