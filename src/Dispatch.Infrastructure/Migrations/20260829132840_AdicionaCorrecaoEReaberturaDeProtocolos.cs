using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCorrecaoEReaberturaDeProtocolos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "corrigido_em",
                table: "protocolos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reaberto_em",
                table: "protocolos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "pedidos_reabertura",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    protocolo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    solicitante_id = table.Column<Guid>(type: "uuid", nullable: false),
                    criado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    decidido_por_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decidido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pedidos_reabertura", x => x.id);
                    table.ForeignKey(
                        name: "fk_pedidos_reabertura_conferentes_solicitante_id",
                        column: x => x.solicitante_id,
                        principalTable: "conferentes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_pedidos_reabertura_protocolos_protocolo_id",
                        column: x => x.protocolo_id,
                        principalTable: "protocolos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_reabertura_protocolo_id",
                table: "pedidos_reabertura",
                column: "protocolo_id");

            migrationBuilder.CreateIndex(
                name: "ix_pedidos_reabertura_solicitante_id",
                table: "pedidos_reabertura",
                column: "solicitante_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pedidos_reabertura");

            migrationBuilder.DropColumn(
                name: "corrigido_em",
                table: "protocolos");

            migrationBuilder.DropColumn(
                name: "reaberto_em",
                table: "protocolos");
        }
    }
}
