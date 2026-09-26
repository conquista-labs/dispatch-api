using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaRegraDoPoolEmConfiguracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADR-0046 (regra do pool). DEFAULT = os padrões decididos pelo dono (limite 5 na mão, ordem
            // obrigatória ligada) — editado à mão sobre o 0/false que o EF gera: a linha única que já
            // existe precisa nascer válida (limite ≥ 1; com 0 nenhum conferente pegaria nada) e com a
            // regra ligada, como o dono pediu (armadilha 5 do CLAUDE.md). O modelo não declara
            // HasDefaultValue de propósito (ver ConfiguracaoConfiguration).
            migrationBuilder.AddColumn<int>(
                name: "limite_de_atos_na_mao",
                table: "configuracao",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<bool>(
                name: "pool_em_ordem_obrigatoria",
                table: "configuracao",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "limite_de_atos_na_mao",
                table: "configuracao");

            migrationBuilder.DropColumn(
                name: "pool_em_ordem_obrigatoria",
                table: "configuracao");
        }
    }
}
