using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarUsuarioEJornadaAoConferente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "jornada_horas",
                table: "conferentes",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<Guid>(
                name: "usuario_id",
                table: "conferentes",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "ix_conferentes_usuario_id",
                table: "conferentes",
                column: "usuario_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_conferentes_usuarios_usuario_id",
                table: "conferentes",
                column: "usuario_id",
                principalTable: "usuarios",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_conferentes_usuarios_usuario_id",
                table: "conferentes");

            migrationBuilder.DropIndex(
                name: "ix_conferentes_usuario_id",
                table: "conferentes");

            migrationBuilder.DropColumn(
                name: "jornada_horas",
                table: "conferentes");

            migrationBuilder.DropColumn(
                name: "usuario_id",
                table: "conferentes");
        }
    }
}
