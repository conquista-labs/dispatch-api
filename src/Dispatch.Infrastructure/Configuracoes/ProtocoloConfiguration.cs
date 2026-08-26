using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class ProtocoloConfiguration : IEntityTypeConfiguration<Protocolo>
{
    public void Configure(EntityTypeBuilder<Protocolo> builder)
    {
        builder.ToTable("protocolos");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Numero).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Etapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Prioridade).HasConversion<string>().HasMaxLength(20);

        // Prazo aqui é opcional (só existe depois de DistribuirProtocolo rodar).
        builder.Property(p => p.Prazo)
            .HasConversion(PrazoConversoes.ParaTextoOpcional)
            .HasColumnName("prazo_tipo")
            .HasMaxLength(20);

        builder.HasOne<TipoAto>()
            .WithMany()
            .HasForeignKey(p => p.TipoAtoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
