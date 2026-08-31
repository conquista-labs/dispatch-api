using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class PedidoReaberturaConfiguration : IEntityTypeConfiguration<PedidoReabertura>
{
    public void Configure(EntityTypeBuilder<PedidoReabertura> builder)
    {
        builder.ToTable("pedidos_reabertura");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CriadoEm);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.DecididoPorId);
        builder.Property(p => p.DecididoEm);

        builder.HasOne<Protocolo>()
            .WithMany()
            .HasForeignKey(p => p.ProtocoloId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Conferente>()
            .WithMany()
            .HasForeignKey(p => p.SolicitanteId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
