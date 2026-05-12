using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcaImporter.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarHistoricoImportacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historico_importacoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    IdPlanilha = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    UrlOriginal = table.Column<string>(type: "TEXT", nullable: false),
                    ImportadaEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    IdExecucao = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    NumeroDfd = table.Column<int>(type: "INTEGER", nullable: false),
                    AnoDfd = table.Column<int>(type: "INTEGER", nullable: false),
                    IdArtefato = table.Column<long>(type: "INTEGER", nullable: false),
                    IdFormalizacaoDemanda = table.Column<long>(type: "INTEGER", nullable: false),
                    TotalMateriais = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_historico_importacoes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_historico_importacoes_IdPlanilha",
                table: "historico_importacoes",
                column: "IdPlanilha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historico_importacoes");
        }
    }
}
