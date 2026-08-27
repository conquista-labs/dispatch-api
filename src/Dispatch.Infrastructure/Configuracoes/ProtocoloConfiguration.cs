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
        builder.Property(p => p.AndamentoEm);
        builder.Property(p => p.LoteImportacaoId);

        // Prazo aqui é opcional (só existe depois de DistribuirProtocolo rodar).
        builder.Property(p => p.Prazo)
            .HasConversion(PrazoConversoes.ParaTextoOpcional)
            .HasColumnName("prazo_tipo")
            .HasMaxLength(20);

        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.MotivoExcecao);

        builder.HasOne<TipoAto>()
            .WithMany()
            .HasForeignKey(p => p.TipoAtoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Conferente>()
            .WithMany()
            .HasForeignKey(p => p.DonoId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LoteImportacao>()
            .WithMany()
            .HasForeignKey(p => p.LoteImportacaoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
