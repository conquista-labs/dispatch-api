using Dispatch.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dispatch.Infrastructure.Configuracoes;

internal sealed class SugestaoRegistroConfiguration : IEntityTypeConfiguration<SugestaoRegistro>
{
    public void Configure(EntityTypeBuilder<SugestaoRegistro> builder)
    {
        // Mesma ideia do CHECK de regras_alcada, só que com 4 variantes em vez de 2: cada uma
        // trava suas próprias colunas preenchidas e todas as outras nulas.
        builder.ToTable("sugestoes", t => t.HasCheckConstraint("ck_sugestoes_payload", """
            (tipo = 'TipoDesconhecido' AND num_nonnulls(tipo_desconhecido_nome_tipo, tipo_desconhecido_nivel_sugerido) = 2
                AND prazo_irreal_equipe_id IS NULL AND escrevente_orfao_escrevente_id IS NULL AND risco_qualidade_tipo_ato_id IS NULL)
            OR (tipo = 'PrazoIrreal' AND num_nonnulls(prazo_irreal_equipe_id, prazo_irreal_etapa, prazo_irreal_prazo_sugerido) = 3
                AND tipo_desconhecido_nome_tipo IS NULL AND escrevente_orfao_escrevente_id IS NULL AND risco_qualidade_tipo_ato_id IS NULL)
            OR (tipo = 'EscreventeOrfao' AND num_nonnulls(escrevente_orfao_escrevente_id, escrevente_orfao_equipe_sugerida_id) = 2
                AND tipo_desconhecido_nome_tipo IS NULL AND prazo_irreal_equipe_id IS NULL AND risco_qualidade_tipo_ato_id IS NULL)
            OR (tipo = 'RiscoQualidade' AND num_nonnulls(risco_qualidade_tipo_ato_id, risco_qualidade_nivel_restrito) = 2
                AND tipo_desconhecido_nome_tipo IS NULL AND prazo_irreal_equipe_id IS NULL AND escrevente_orfao_escrevente_id IS NULL)
            """));

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Chave).IsRequired().HasMaxLength(200);
        builder.Property(s => s.Tipo).HasConversion<string>().HasMaxLength(30);

        builder.Property(s => s.TipoDesconhecidoNomeTipo).HasMaxLength(200);
        builder.Property(s => s.TipoDesconhecidoNivelSugerido).HasConversion<string>().HasMaxLength(20);

        builder.Property(s => s.PrazoIrrealEquipeId);
        builder.Property(s => s.PrazoIrrealEtapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.PrazoIrrealPrazoSugerido).HasConversion<string>().HasMaxLength(20);

        builder.Property(s => s.EscreventeOrfaoEscreventeId);
        builder.Property(s => s.EscreventeOrfaoEquipeSugeridaId);

        builder.Property(s => s.RiscoQualidadeTipoAtoId);
        builder.Property(s => s.RiscoQualidadeNivelRestrito).HasConversion<string>().HasMaxLength(20);

        builder.Property(s => s.Evidencia).IsRequired();
        builder.Property(s => s.Ocorrencias);
        builder.Property(s => s.IndiceConfianca);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.CriadaEm);
        builder.Property(s => s.AtualizadaEm);
        builder.Property(s => s.DecididaEm);
        builder.Property(s => s.DescartarAte);
    }
}
