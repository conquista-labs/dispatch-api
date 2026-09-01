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
        // como defesa a mais na camada de dados. O lado do alvo não dá mais pra travar só com
        // num_nonnulls (PorTodosOsAtos não tem payload, PorEquipeDeEscrevente tem payload
        // nulo-válido) — o CHECK segue o discriminador AlvoTipo, cada variante travando as
        // colunas das outras três famílias como nulas (alvo_equipe_id fica de fora da lista
        // "deve ser nulo" na variante Equipe, porque nulo ali é "sem equipe", não ausência).
        builder.ToTable("regras_alcada", t =>
        {
            t.HasCheckConstraint("ck_regras_alcada_sujeito", "num_nonnulls(sujeito_conferente_id, sujeito_nivel) = 1");
            t.HasCheckConstraint("ck_regras_alcada_alvo", """
                (alvo_tipo = 'Etapa' AND alvo_etapa IS NOT NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL AND alvo_grupo_tipo_ato IS NULL)
                OR (alvo_tipo = 'TipoAto' AND alvo_tipo_ato_id IS NOT NULL AND alvo_etapa IS NULL AND alvo_equipe_id IS NULL AND alvo_grupo_tipo_ato IS NULL)
                OR (alvo_tipo = 'Equipe' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_grupo_tipo_ato IS NULL)
                OR (alvo_tipo = 'TodosOsAtos' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL AND alvo_grupo_tipo_ato IS NULL)
                OR (alvo_tipo = 'Grupo' AND alvo_grupo_tipo_ato IS NOT NULL AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL)
                """);
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.SujeitoNivel).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.AlvoTipo).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.AlvoEtapa).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.AlvoGrupoTipoAto).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Permissao).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Origem).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Ativa);
    }
}
