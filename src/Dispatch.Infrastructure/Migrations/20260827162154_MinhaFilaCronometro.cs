using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MinhaFilaCronometro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "concluido_em",
                table: "protocolos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "iniciado_em",
                table: "protocolos",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concluido_em",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "iniciado_em",
                table: "protocolos");
        }
    }
}
