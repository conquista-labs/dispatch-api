using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAjustesDeDuracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ajustes_de_duracao",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ajustado_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ajustado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    duracao_anterior = table.Column<TimeSpan>(type: "interval", nullable: true),
                    duracao_nova = table.Column<TimeSpan>(type: "interval", nullable: false),
                    motivo = table.Column<string>(type: "text", nullable: true),
                    protocolo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ajustes_de_duracao", x => x.id);
                    table.ForeignKey(
                        name: "fk_ajustes_de_duracao_protocolos_protocolo_id",
                        column: x => x.protocolo_id,
                        principalTable: "protocolos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ajustes_de_duracao_protocolo_id",
                table: "ajustes_de_duracao",
                column: "protocolo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ajustes_de_duracao");
        }
    }
}
