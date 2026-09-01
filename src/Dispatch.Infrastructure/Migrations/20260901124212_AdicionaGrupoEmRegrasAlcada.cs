using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaGrupoEmRegrasAlcada : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada");

            migrationBuilder.AddColumn<string>(
                name: "alvo_grupo_tipo_ato",
                table: "regras_alcada",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada",
                sql: "(alvo_tipo = 'Etapa' AND alvo_etapa IS NOT NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL AND alvo_grupo_tipo_ato IS NULL)\nOR (alvo_tipo = 'TipoAto' AND alvo_tipo_ato_id IS NOT NULL AND alvo_etapa IS NULL AND alvo_equipe_id IS NULL AND alvo_grupo_tipo_ato IS NULL)\nOR (alvo_tipo = 'Equipe' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_grupo_tipo_ato IS NULL)\nOR (alvo_tipo = 'TodosOsAtos' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL AND alvo_grupo_tipo_ato IS NULL)\nOR (alvo_tipo = 'Grupo' AND alvo_grupo_tipo_ato IS NOT NULL AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada");

            migrationBuilder.DropColumn(
                name: "alvo_grupo_tipo_ato",
                table: "regras_alcada");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada",
                sql: "(alvo_tipo = 'Etapa' AND alvo_etapa IS NOT NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL)\nOR (alvo_tipo = 'TipoAto' AND alvo_tipo_ato_id IS NOT NULL AND alvo_etapa IS NULL AND alvo_equipe_id IS NULL)\nOR (alvo_tipo = 'Equipe' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL)\nOR (alvo_tipo = 'TodosOsAtos' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL)");
        }
    }
}
