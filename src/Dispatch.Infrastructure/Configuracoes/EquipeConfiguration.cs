using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class EquipeConfiguration : IEntityTypeConfiguration<Equipe>
{
    public void Configure(EntityTypeBuilder<Equipe> builder)
    {
        builder.ToTable("equipes");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Nome).IsRequired().HasMaxLength(200);

        builder.Property(e => e.PrazoPreConferencia)
            .HasConversion(PrazoConversoes.ParaTexto)
            .HasColumnName("prazo_pre_tipo")
            .HasMaxLength(20);

        builder.Property(e => e.PrazoPosConferencia)
            .HasConversion(PrazoConversoes.ParaTexto)
            .HasColumnName("prazo_pos_tipo")
            .HasMaxLength(20);
    }
}
