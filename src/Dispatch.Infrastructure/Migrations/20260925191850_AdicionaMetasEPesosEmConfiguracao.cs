using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaMetasEPesosEmConfiguracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // DEFAULT = os valores que eram constantes até aqui (RF-42b: metas 95%/90%; RF-46: pesos
            // 40/30/20/10) — editado à mão sobre o 0/0.0 que o EF gera: a linha única que já existe
            // precisa nascer com pesos somando 100 e o score sem mudar (armadilha 5 do CLAUDE.md).
            // O modelo não declara HasDefaultValue de propósito (ver ConfiguracaoConfiguration).
            migrationBuilder.AddColumn<double>(
                name: "meta_aprovado_na_primeira",
                table: "configuracao",
                type: "double precision",
                nullable: false,
                defaultValue: 0.90);

            migrationBuilder.AddColumn<double>(
                name: "meta_no_prazo",
                table: "configuracao",
                type: "double precision",
                nullable: false,
                defaultValue: 0.95);

            migrationBuilder.AddColumn<int>(
                name: "peso_complexidade",
                table: "configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "peso_prazo",
                table: "configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "peso_qualidade",
                table: "configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 20);

            migrationBuilder.AddColumn<int>(
                name: "peso_volume",
                table: "configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 40);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "meta_aprovado_na_primeira",
                table: "configuracao");

            migrationBuilder.DropColumn(
                name: "meta_no_prazo",
                table: "configuracao");

            migrationBuilder.DropColumn(
                name: "peso_complexidade",
                table: "configuracao");

            migrationBuilder.DropColumn(
                name: "peso_prazo",
                table: "configuracao");

            migrationBuilder.DropColumn(
                name: "peso_qualidade",
                table: "configuracao");

            migrationBuilder.DropColumn(
                name: "peso_volume",
                table: "configuracao");
        }
    }
}
