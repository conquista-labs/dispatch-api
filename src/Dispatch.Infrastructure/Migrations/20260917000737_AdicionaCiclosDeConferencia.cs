using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCiclosDeConferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ciclos_conferencia",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    conferente_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iniciado_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    concluido_em = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    protocolo_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ciclos_conferencia", x => x.id);
                    table.ForeignKey(
                        name: "fk_ciclos_conferencia_protocolos_protocolo_id",
                        column: x => x.protocolo_id,
                        principalTable: "protocolos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ciclos_conferencia_conferente_id",
                table: "ciclos_conferencia",
                column: "conferente_id");

            migrationBuilder.CreateIndex(
                name: "ix_ciclos_conferencia_protocolo_id",
                table: "ciclos_conferencia",
                column: "protocolo_id");

            // Backfill: protocolos que já foram reabertos antes desta migration só tinham um
            // TimeSpan cego (tempo_acumulado_anterior), sem saber de quem era cada ciclo. Não dá
            // pra reconstruir o histórico exato (não sabemos os horários reais de início/fim de
            // cada ciclo passado), mas dropar a coluna sem migrar apagaria esse tempo da Duracao
            // final e do Dashboard sem mais nem menos — melhor um único ciclo sintético
            // atribuído ao dono atual (o cenário confirmado como esmagadoramente comum: o mesmo
            // conferente refaz a conferência, só troca quando ele sai da escala) do que perder o
            // dado. COALESCE(iniciado_em, reaberto_em) cobre os dois casos possíveis no momento
            // desta migration: já concluído nesse ciclo (iniciado_em existe) ou reaberto e ainda
            // não reiniciado pelo conferente (iniciado_em nulo desde o fix anterior, usa
            // reaberto_em como âncora). Sem dono ou sem nenhuma âncora de horário, não tem como
            // atribuir a ninguém — fica de fora (tempo perdido, caso raro e já órfão antes desta
            // migration).
            migrationBuilder.Sql("""
                INSERT INTO ciclos_conferencia (conferente_id, iniciado_em, concluido_em, protocolo_id)
                SELECT
                    dono_id,
                    COALESCE(iniciado_em, reaberto_em) - tempo_acumulado_anterior,
                    COALESCE(iniciado_em, reaberto_em),
                    id
                FROM protocolos
                WHERE tempo_acumulado_anterior > interval '0 seconds'
                  AND dono_id IS NOT NULL
                  AND COALESCE(iniciado_em, reaberto_em) IS NOT NULL;
                """);

            migrationBuilder.DropColumn(
                name: "tempo_acumulado_anterior",
                table: "protocolos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "tempo_acumulado_anterior",
                table: "protocolos",
                type: "interval",
                nullable: false,
                defaultValue: new TimeSpan(0, 0, 0, 0, 0));

            // Simétrico ao backfill do Up: reconstitui a soma cega antes de derrubar a tabela de
            // ciclos, pra reverter esta migration não jogar fora o tempo acumulado de quem já
            // tinha reaberto um protocolo depois que ela rodou.
            migrationBuilder.Sql("""
                UPDATE protocolos
                SET tempo_acumulado_anterior = ciclos.soma
                FROM (
                    SELECT protocolo_id, SUM(concluido_em - iniciado_em) AS soma
                    FROM ciclos_conferencia
                    GROUP BY protocolo_id
                ) AS ciclos
                WHERE protocolos.id = ciclos.protocolo_id;
                """);

            migrationBuilder.DropTable(
                name: "ciclos_conferencia");
        }
    }
}
