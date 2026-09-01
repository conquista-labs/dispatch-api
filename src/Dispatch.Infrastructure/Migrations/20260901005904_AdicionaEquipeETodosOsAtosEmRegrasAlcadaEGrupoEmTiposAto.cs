using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaEquipeETodosOsAtosEmRegrasAlcadaEGrupoEmTiposAto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada");

            migrationBuilder.AddColumn<string>(
                name: "grupo",
                table: "tipos_ato",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "alvo_equipe_id",
                table: "regras_alcada",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "alvo_tipo",
                table: "regras_alcada",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Backfill obrigatório: toda regra já existente tinha alvo_etapa OU
            // alvo_tipo_ato_id preenchido (CHECK antigo garantia isso) — sem isso, o CHECK
            // novo (que exige alvo_tipo = 'Etapa'/'TipoAto' coerente) rejeitaria qualquer
            // linha pré-existente, quebrando a migration em qualquer ambiente com regra já
            // cadastrada (produção incluída).
            migrationBuilder.Sql("""
                UPDATE regras_alcada SET alvo_tipo = 'Etapa' WHERE alvo_etapa IS NOT NULL;
                UPDATE regras_alcada SET alvo_tipo = 'TipoAto' WHERE alvo_tipo_ato_id IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada",
                sql: "(alvo_tipo = 'Etapa' AND alvo_etapa IS NOT NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL)\nOR (alvo_tipo = 'TipoAto' AND alvo_tipo_ato_id IS NOT NULL AND alvo_etapa IS NULL AND alvo_equipe_id IS NULL)\nOR (alvo_tipo = 'Equipe' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL)\nOR (alvo_tipo = 'TodosOsAtos' AND alvo_etapa IS NULL AND alvo_tipo_ato_id IS NULL AND alvo_equipe_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada");

            migrationBuilder.DropColumn(
                name: "grupo",
                table: "tipos_ato");

            migrationBuilder.DropColumn(
                name: "alvo_equipe_id",
                table: "regras_alcada");

            migrationBuilder.DropColumn(
                name: "alvo_tipo",
                table: "regras_alcada");

            migrationBuilder.AddCheckConstraint(
                name: "ck_regras_alcada_alvo",
                table: "regras_alcada",
                sql: "num_nonnulls(alvo_etapa, alvo_tipo_ato_id) = 1");
        }
    }
}
