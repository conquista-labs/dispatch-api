using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class UsuarioTotpConfiguration : IEntityTypeConfiguration<UsuarioTotp>
{
    public void Configure(EntityTypeBuilder<UsuarioTotp> builder)
    {
        builder.ToTable("usuarios_totp");
        builder.HasKey(u => u.UsuarioId);
        builder.Property(u => u.SegredoCifrado).IsRequired();
        builder.Property(u => u.ConfirmadoEm);
        builder.Property(u => u.UltimoContadorAceito);
        builder.Property(u => u.TentativasFalhas);
        builder.Property(u => u.BloqueadoAte);
        builder.Property(u => u.TokenRecuperacaoHash);
        builder.Property(u => u.TokenRecuperacaoExpiraEm);
        builder.Property(u => u.CriadoEm);

        builder.HasOne<Usuario>()
            .WithMany()
            .HasForeignKey(u => u.UsuarioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
