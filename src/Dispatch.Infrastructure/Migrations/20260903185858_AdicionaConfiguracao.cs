using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaConfiguracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "configuracao",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faixa_atencao = table.Column<TimeSpan>(type: "interval", nullable: false),
                    faixa_urgente = table.Column<TimeSpan>(type: "interval", nullable: false),
                    limite_de_atos_simultaneos = table.Column<int>(type: "integer", nullable: false),
                    janela_de_correcao = table.Column<TimeSpan>(type: "interval", nullable: false),
                    dias_de_memoria_descarte = table.Column<int>(type: "integer", nullable: false),
                    tempo_medio_por_ato_minutos = table.Column<double>(type: "double precision", nullable: false),
                    limiar_tipo_desconhecido = table.Column<int>(type: "integer", nullable: false),
                    limiar_prazo_irreal_casos = table.Column<int>(type: "integer", nullable: false),
                    limiar_prazo_irreal_estouro = table.Column<double>(type: "double precision", nullable: false),
                    limiar_escrevente_orfao = table.Column<int>(type: "integer", nullable: false),
                    limiar_risco_qualidade_casos = table.Column<int>(type: "integer", nullable: false),
                    limiar_risco_qualidade_reprovacao = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_configuracao", x => x.id);
                });

            // Linha única, semeada com os mesmos valores que eram hardcoded antes desta tabela
            // existir (ver dispatch-api/CLAUDE.md) — sem isso ConfiguracaoRepository.ObterAsync
            // quebra em runtime contra uma tabela vazia.
            migrationBuilder.InsertData(
                table: "configuracao",
                columns: new[]
                {
                    "id", "faixa_atencao", "faixa_urgente", "limite_de_atos_simultaneos", "janela_de_correcao",
                    "dias_de_memoria_descarte", "tempo_medio_por_ato_minutos", "limiar_tipo_desconhecido",
                    "limiar_prazo_irreal_casos", "limiar_prazo_irreal_estouro", "limiar_escrevente_orfao",
                    "limiar_risco_qualidade_casos", "limiar_risco_qualidade_reprovacao"
                },
                values: new object[]
                {
                    Guid.Parse("00000000-0000-0000-0000-000000000001"), TimeSpan.FromHours(4), TimeSpan.FromMinutes(60), 1,
                    TimeSpan.FromMinutes(15), 30, 18.0, 5, 8, 0.6, 3, 6, 0.5
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "configuracao");
        }
    }
}
