using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class UsuarioConfiguration : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("usuarios");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Nome).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.SenhaHash).IsRequired();
        builder.Property(u => u.Papel).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Ativo);
        // Explícito de propósito — mesma armadilha já documentada (propriedade só-com-setter-
        // privado falha o constructor binding do EF Core em tempo de design se não declarada
        // aqui, mesmo padrão de Ativo acima).
        builder.Property(u => u.TentativasLoginFalhas);
        builder.Property(u => u.BloqueadoAte);
        // Mesma armadilha de propriedade com setter privado: declarar explícito pro EF ligar.
        builder.Property(u => u.TrocarSenhaNoProximoAcesso).HasDefaultValue(false);

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
