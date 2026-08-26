using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InicializarSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conferentes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    na_escala = table.Column<bool>(type: "boolean", nullable: false),
                    carga_atual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conferentes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    prazo_pre_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prazo_pos_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equipes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regras_alcada",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sujeito_conferente_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sujeito_nivel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    alvo_etapa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    alvo_tipo_ato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permissao = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ativa = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_regras_alcada", x => x.id);
                    table.CheckConstraint("ck_regras_alcada_alvo", "num_nonnulls(alvo_etapa, alvo_tipo_ato_id) = 1");
                    table.CheckConstraint("ck_regras_alcada_sujeito", "num_nonnulls(sujeito_conferente_id, sujeito_nivel) = 1");
                });

            migrationBuilder.CreateTable(
                name: "tipos_ato",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tipos_ato", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "escreventes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    equipe_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_escreventes", x => x.id);
                    table.ForeignKey(
                        name: "fk_escreventes_equipes_equipe_id",
                        column: x => x.equipe_id,
                        principalTable: "equipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "protocolos",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    tipo_ato_id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prioridade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    prazo_tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    vencimento_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_protocolos", x => x.id);
                    table.ForeignKey(
                        name: "fk_protocolos_tipos_ato_tipo_ato_id",
                        column: x => x.tipo_ato_id,
                        principalTable: "tipos_ato",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_escreventes_equipe_id",
                table: "escreventes",
                column: "equipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_protocolos_tipo_ato_id",
                table: "protocolos",
                column: "tipo_ato_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conferentes");

            migrationBuilder.DropTable(
                name: "escreventes");

            migrationBuilder.DropTable(
                name: "protocolos");

            migrationBuilder.DropTable(
                name: "regras_alcada");

            migrationBuilder.DropTable(
                name: "equipes");

            migrationBuilder.DropTable(
                name: "tipos_ato");
        }
    }
}
