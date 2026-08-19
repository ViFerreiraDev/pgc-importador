using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcaImporter.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class RegistrarExclusaoDfdHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DfdExcluidoEm",
                table: "historico_importacoes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DfdExcluidoPorLogin",
                table: "historico_importacoes",
                type: "TEXT",
                maxLength: 60,
                nullable: true);

            // Reconcilia exclusões feitas antes desta migração. Para cada passagem de um
            // link pela lixeira, associa a importação bem-sucedida mais recente que já
            // existia naquele instante. Assim uma reimportação posterior não é invalidada.
            migrationBuilder.Sql(
                """
                WITH "pares" AS (
                    SELECT
                        "l"."ExcluidoEm",
                        "l"."ExcluidoPorLogin",
                        (
                            SELECT "h"."Id"
                            FROM "historico_importacoes" AS "h"
                            WHERE "h"."IdPlanilha" = "l"."IdPlanilha"
                              AND "h"."Sucesso" = 1
                              AND "h"."ImportadaEm" <= "l"."ExcluidoEm"
                            ORDER BY "h"."ImportadaEm" DESC, "h"."Id" DESC
                            LIMIT 1
                        ) AS "HistoricoId"
                    FROM "lista_link" AS "l"
                    WHERE "l"."ExcluidoEm" IS NOT NULL
                      AND "l"."ImportadoEm" IS NOT NULL
                )
                UPDATE "historico_importacoes"
                SET
                    "DfdExcluidoEm" = (
                        SELECT "p"."ExcluidoEm"
                        FROM "pares" AS "p"
                        WHERE "p"."HistoricoId" = "historico_importacoes"."Id"
                        LIMIT 1
                    ),
                    "DfdExcluidoPorLogin" = (
                        SELECT "p"."ExcluidoPorLogin"
                        FROM "pares" AS "p"
                        WHERE "p"."HistoricoId" = "historico_importacoes"."Id"
                        LIMIT 1
                    )
                WHERE "Id" IN (
                    SELECT "HistoricoId" FROM "pares" WHERE "HistoricoId" IS NOT NULL
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_historico_importacoes_DfdExcluidoEm",
                table: "historico_importacoes",
                column: "DfdExcluidoEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_historico_importacoes_DfdExcluidoEm",
                table: "historico_importacoes");

            migrationBuilder.DropColumn(
                name: "DfdExcluidoEm",
                table: "historico_importacoes");

            migrationBuilder.DropColumn(
                name: "DfdExcluidoPorLogin",
                table: "historico_importacoes");
        }
    }
}
