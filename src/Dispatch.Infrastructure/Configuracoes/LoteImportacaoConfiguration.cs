using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class LoteImportacaoConfiguration : IEntityTypeConfiguration<LoteImportacao>
{
    public void Configure(EntityTypeBuilder<LoteImportacao> builder)
    {
        builder.ToTable("lotes_importacao");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Etapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(l => l.LinhaDeCorte);
        builder.Property(l => l.ImportadoEm);
        builder.Property(l => l.TotalLinhas);
    }
}
