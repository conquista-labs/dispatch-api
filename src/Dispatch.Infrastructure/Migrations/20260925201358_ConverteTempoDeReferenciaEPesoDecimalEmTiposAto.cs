using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dispatch.Infrastructure.Migrations
{
    /// <summary>
    /// Fatia 5 do Dashboard v2 (RF-34a, RF-34f, RF-46c). Duas mudanças em <c>tipos_ato</c>:
    /// <list type="bullet">
    /// <item><c>peso_complexidade</c> de inteiro (≥ 1) para <c>numeric(3,2)</c> 0,50–2,50, CONVERTENDO o dado
    /// existente pelo mapa do dono (25/09/2026): 1→1,00 · 2→1,25 · 3→1,50 · 4→1,75 · 5→2,00; qualquer
    /// outro valor é limitado à faixa (≤ 0 → 0,50; ≥ 6 → 2,50). O <c>AlterColumn</c> que o EF gera faria um
    /// cast direto (3 → 3,00, fora da faixa, e ≥ 10 estouraria o numeric(3,2)) — por isso SQL explícito
    /// com <c>USING</c>, na mesma instrução que troca o tipo.</item>
    /// <item><c>tempo_referencia_minutos int NULL</c> — o valor informado pelo administrador; nulo = usa
    /// mediana/estimativa. Sem backfill: nasce nulo em todos (ninguém informou nada ainda).</item>
    /// </list>
    /// Os CHECK entram depois da conversão (armadilha 5: CHECK novo só depois do dado estar dentro dele).
    /// O DEFAULT 1 que a migration AdicionaPesoDeComplexidadeEmTiposAto deixou no banco sai: o EF sempre
    /// envia o peso, e o default nunca esteve no modelo.
    /// </summary>
    public partial class ConverteTempoDeReferenciaEPesoDecimalEmTiposAto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                ALTER TABLE tipos_ato ALTER COLUMN peso_complexidade DROP DEFAULT;
                ALTER TABLE tipos_ato ALTER COLUMN peso_complexidade TYPE numeric(3,2) USING (
                    CASE peso_complexidade
                        WHEN 1 THEN 1.00
                        WHEN 2 THEN 1.25
                        WHEN 3 THEN 1.50
                        WHEN 4 THEN 1.75
                        WHEN 5 THEN 2.00
                        ELSE CASE WHEN peso_complexidade <= 0 THEN 0.50 ELSE 2.50 END
                    END);
                """);

            migrationBuilder.AddColumn<int>(
                name: "tempo_referencia_minutos",
                table: "tipos_ato",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipos_ato_peso_complexidade",
                table: "tipos_ato",
                sql: "peso_complexidade BETWEEN 0.50 AND 2.50 AND mod(peso_complexidade * 100, 5) = 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_tipos_ato_tempo_referencia_minutos",
                table: "tipos_ato",
                sql: "tempo_referencia_minutos IS NULL OR tempo_referencia_minutos BETWEEN 2 AND 240");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Volta ao inteiro pelo mapa inverso, com os valores intermediários indo para o inteiro mais próximo
        /// no mesmo mapa (1,10 → 1; 1,40 → 3; acima de 2,00 → 5). Perde a precisão decimal e o tempo informado
        /// — inevitável num rollback de tipo.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_tipos_ato_peso_complexidade",
                table: "tipos_ato");

            migrationBuilder.DropCheckConstraint(
                name: "ck_tipos_ato_tempo_referencia_minutos",
                table: "tipos_ato");

            migrationBuilder.DropColumn(
                name: "tempo_referencia_minutos",
                table: "tipos_ato");

            migrationBuilder.Sql("""
                ALTER TABLE tipos_ato ALTER COLUMN peso_complexidade TYPE integer USING (
                    CASE
                        WHEN peso_complexidade < 1.125 THEN 1
                        WHEN peso_complexidade < 1.375 THEN 2
                        WHEN peso_complexidade < 1.625 THEN 3
                        WHEN peso_complexidade < 1.875 THEN 4
                        ELSE 5
                    END);
                ALTER TABLE tipos_ato ALTER COLUMN peso_complexidade SET DEFAULT 1;
                """);
        }
    }
}
