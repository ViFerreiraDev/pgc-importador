using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcaImporter.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCatalogoMateriais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "catalogo_materiais",
                columns: table => new
                {
                    CodigoItem = table.Column<int>(type: "INTEGER", nullable: false),
                    CodigoGrupo = table.Column<int>(type: "INTEGER", nullable: false),
                    NomeGrupo = table.Column<string>(type: "TEXT", nullable: false),
                    CodigoClasse = table.Column<int>(type: "INTEGER", nullable: false),
                    NomeClasse = table.Column<string>(type: "TEXT", nullable: false),
                    CodigoPdm = table.Column<int>(type: "INTEGER", nullable: false),
                    NomePdm = table.Column<string>(type: "TEXT", nullable: false),
                    DescricaoItem = table.Column<string>(type: "TEXT", nullable: false),
                    StatusItem = table.Column<bool>(type: "INTEGER", nullable: false),
                    ItemSustentavel = table.Column<bool>(type: "INTEGER", nullable: false),
                    CodigoNcm = table.Column<string>(type: "TEXT", nullable: true),
                    AtualizadoEmFonte = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    SincronizadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogo_materiais", x => x.CodigoItem);
                });

            migrationBuilder.CreateIndex(
                name: "IX_catalogo_materiais_CodigoClasse",
                table: "catalogo_materiais",
                column: "CodigoClasse");

            migrationBuilder.CreateIndex(
                name: "IX_catalogo_materiais_CodigoPdm",
                table: "catalogo_materiais",
                column: "CodigoPdm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalogo_materiais");
        }
    }
}
