using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EscreventeDoProtocolo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "escrevente_id",
                table: "protocolos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_protocolos_escrevente_id",
                table: "protocolos",
                column: "escrevente_id");

            migrationBuilder.AddForeignKey(
                name: "fk_protocolos_escreventes_escrevente_id",
                table: "protocolos",
                column: "escrevente_id",
                principalTable: "escreventes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_protocolos_escreventes_escrevente_id",
                table: "protocolos");

            migrationBuilder.DropIndex(
                name: "ix_protocolos_escrevente_id",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "escrevente_id",
                table: "protocolos");
        }
    }
}
