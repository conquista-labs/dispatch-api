using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class TipoAtoConfiguration : IEntityTypeConfiguration<TipoAto>
{
    public void Configure(EntityTypeBuilder<TipoAto> builder)
    {
        builder.ToTable("tipos_ato");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Nome).IsRequired().HasMaxLength(200);
    }
}
