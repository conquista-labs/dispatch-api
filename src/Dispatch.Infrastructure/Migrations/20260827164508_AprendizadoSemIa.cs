using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AprendizadoSemIa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_ato_nome_original",
                table: "protocolos",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sugestoes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    tipo_desconhecido_nome_tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tipo_desconhecido_nivel_sugerido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    prazo_irreal_equipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prazo_irreal_etapa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    prazo_irreal_prazo_sugerido = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    escrevente_orfao_escrevente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    escrevente_orfao_equipe_sugerida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    risco_qualidade_tipo_ato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    risco_qualidade_nivel_restrito = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    evidencia = table.Column<string>(type: "text", nullable: false),
                    ocorrencias = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    criada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    atualizada_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decidida_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    descartar_ate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sugestoes", x => x.id);
                    table.CheckConstraint("ck_sugestoes_payload", "(tipo = 'TipoDesconhecido' AND num_nonnulls(tipo_desconhecido_nome_tipo, tipo_desconhecido_nivel_sugerido) = 2\n    AND prazo_irreal_equipe_id IS NULL AND escrevente_orfao_escrevente_id IS NULL AND risco_qualidade_tipo_ato_id IS NULL)\nOR (tipo = 'PrazoIrreal' AND num_nonnulls(prazo_irreal_equipe_id, prazo_irreal_etapa, prazo_irreal_prazo_sugerido) = 3\n    AND tipo_desconhecido_nome_tipo IS NULL AND escrevente_orfao_escrevente_id IS NULL AND risco_qualidade_tipo_ato_id IS NULL)\nOR (tipo = 'EscreventeOrfao' AND num_nonnulls(escrevente_orfao_escrevente_id, escrevente_orfao_equipe_sugerida_id) = 2\n    AND tipo_desconhecido_nome_tipo IS NULL AND prazo_irreal_equipe_id IS NULL AND risco_qualidade_tipo_ato_id IS NULL)\nOR (tipo = 'RiscoQualidade' AND num_nonnulls(risco_qualidade_tipo_ato_id, risco_qualidade_nivel_restrito) = 2\n    AND tipo_desconhecido_nome_tipo IS NULL AND prazo_irreal_equipe_id IS NULL AND escrevente_orfao_escrevente_id IS NULL)");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sugestoes");

            migrationBuilder.DropColumn(
                name: "tipo_ato_nome_original",
                table: "protocolos");
        }
    }
}
