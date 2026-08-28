using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaAtribuidoEmERegraAplicadaEmProtocolos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "atribuido_em",
                table: "protocolos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "regra_aplicada_id",
                table: "protocolos",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "atribuido_em",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "regra_aplicada_id",
                table: "protocolos");
        }
    }
}
