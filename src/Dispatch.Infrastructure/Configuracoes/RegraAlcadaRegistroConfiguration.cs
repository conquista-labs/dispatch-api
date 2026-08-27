using Dispatch.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

internal sealed class RegraAlcadaRegistroConfiguration : IEntityTypeConfiguration<RegraAlcadaRegistro>
{
    public void Configure(EntityTypeBuilder<RegraAlcadaRegistro> builder)
    {
        // num_nonnulls é função nativa do Postgres: garante no banco, não só no C#, que uma
        // regra é "por pessoa" OU "por nível" (nunca os dois, nunca nenhum) — o mesmo
        // invariante que SujeitoAlcada trava em tempo de compilação no Domain, só que aqui
        // como defesa a mais na camada de dados.
        builder.ToTable("regras_alcada", t =>
        {
            t.HasCheckConstraint("ck_regras_alcada_sujeito", "num_nonnulls(sujeito_conferente_id, sujeito_nivel) = 1");
            t.HasCheckConstraint("ck_regras_alcada_alvo", "num_nonnulls(alvo_etapa, alvo_tipo_ato_id) = 1");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.SujeitoNivel).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.AlvoEtapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Permissao).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Origem).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Ativa);
    }
}
