using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaIndicesEmStatusENumeroDeProtocolos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_protocolos_numero",
                table: "protocolos",
                column: "numero");

            migrationBuilder.CreateIndex(
                name: "ix_protocolos_status",
                table: "protocolos",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_protocolos_numero",
                table: "protocolos");

            migrationBuilder.DropIndex(
                name: "ix_protocolos_status",
                table: "protocolos");
        }
    }
}
