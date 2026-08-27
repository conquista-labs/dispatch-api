using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoteImportacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lote_importacao_id",
                table: "protocolos",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "lotes_importacao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    etapa = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    linha_de_corte = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    importado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_linhas = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lotes_importacao", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_protocolos_lote_importacao_id",
                table: "protocolos",
                column: "lote_importacao_id");

            migrationBuilder.AddForeignKey(
                name: "fk_protocolos_lotes_importacao_lote_importacao_id",
                table: "protocolos",
                column: "lote_importacao_id",
                principalTable: "lotes_importacao",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_protocolos_lotes_importacao_lote_importacao_id",
                table: "protocolos");

            migrationBuilder.DropTable(
                name: "lotes_importacao");

            migrationBuilder.DropIndex(
                name: "ix_protocolos_lote_importacao_id",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "lote_importacao_id",
                table: "protocolos");
        }
    }
}
