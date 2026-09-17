using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaHistoricoDePausas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pausas_conferencia",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    pausado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    retomado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    protocolo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pausas_conferencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_pausas_conferencia_protocolos_protocolo_id",
                        column: x => x.protocolo_id,
                        principalTable: "protocolos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pausas_conferencia_protocolo_id",
                table: "pausas_conferencia",
                column: "protocolo_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pausas_conferencia");
        }
    }
}
