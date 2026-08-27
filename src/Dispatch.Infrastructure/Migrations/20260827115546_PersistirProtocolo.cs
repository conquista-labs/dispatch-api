using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PersistirProtocolo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "dono_id",
                table: "protocolos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "motivo_excecao",
                table: "protocolos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "protocolos",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "ix_protocolos_dono_id",
                table: "protocolos",
                column: "dono_id");

            migrationBuilder.AddForeignKey(
                name: "fk_protocolos_conferentes_dono_id",
                table: "protocolos",
                column: "dono_id",
                principalTable: "conferentes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_protocolos_conferentes_dono_id",
                table: "protocolos");

            migrationBuilder.DropIndex(
                name: "ix_protocolos_dono_id",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "dono_id",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "motivo_excecao",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "status",
                table: "protocolos");
        }
    }
}
