using Dispatch.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

public sealed class TipoAtoConfiguration : IEntityTypeConfiguration<TipoAto>
{
    public void Configure(EntityTypeBuilder<TipoAto> builder)
    {
        // Os dois CHECK repetem no banco as regras de PesoDeComplexidade e TempoDeReferencia.ValidarInformado:
        // o construtor de TipoAto (usado pelo EF na leitura) lança com valor inválido, então um UPDATE à mão
        // fora da regra derrubaria toda leitura do catálogo — melhor o banco recusar na escrita.
        builder.ToTable("tipos_ato", t =>
        {
            t.HasCheckConstraint(
                "ck_tipos_ato_peso_complexidade",
                "peso_complexidade BETWEEN 0.50 AND 2.50 AND mod(peso_complexidade * 100, 5) = 0");
            t.HasCheckConstraint(
                "ck_tipos_ato_tempo_referencia_minutos",
                "tempo_referencia_minutos IS NULL OR tempo_referencia_minutos BETWEEN 2 AND 240");
        });
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Nome).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Grupo).HasConversion<string>().HasMaxLength(20);
        // numeric(3,2): 0,50–2,50 cabe com folga (até 9,99) e o valor volta exato (decimal, não double).
        builder.Property(t => t.PesoComplexidade).HasPrecision(3, 2);
        builder.Property(t => t.TempoReferenciaMinutos);
    }
}
