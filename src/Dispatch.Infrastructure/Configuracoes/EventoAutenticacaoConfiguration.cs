using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class EventoAutenticacaoConfiguration : IEntityTypeConfiguration<EventoAutenticacao>
{
    public void Configure(EntityTypeBuilder<EventoAutenticacao> builder)
    {
        builder.ToTable("eventos_autenticacao");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.CriadoEm);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(e => e.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
