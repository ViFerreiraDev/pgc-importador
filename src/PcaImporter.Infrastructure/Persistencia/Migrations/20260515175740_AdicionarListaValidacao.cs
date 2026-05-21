using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcaImporter.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarListaValidacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lista_link",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Rotulo = table.Column<string>(type: "TEXT", nullable: true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    IdPlanilha = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Estado = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    TotalMateriais = table.Column<int>(type: "INTEGER", nullable: true),
                    MensagemErro = table.Column<string>(type: "TEXT", nullable: true),
                    ValidadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CriadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CriadoPorLogin = table.Column<string>(type: "TEXT", nullable: true),
                    ExcluidoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExcluidoPorLogin = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lista_link", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "lista_item",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LinkId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    FingerprintVersao = table.Column<int>(type: "INTEGER", nullable: false),
                    Local = table.Column<string>(type: "TEXT", nullable: true),
                    Campo = table.Column<string>(type: "TEXT", nullable: true),
                    Linha = table.Column<int>(type: "INTEGER", nullable: true),
                    Codigo = table.Column<string>(type: "TEXT", nullable: true),
                    DeltaPct = table.Column<double>(type: "REAL", nullable: true),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CriadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lista_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lista_item_lista_link_LinkId",
                        column: x => x.LinkId,
                        principalTable: "lista_link",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lista_item_revisao",
                columns: table => new
                {
                    ItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    RevisadoPorLogin = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    RevisadoEm = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lista_item_revisao", x => new { x.ItemId, x.RevisadoPorLogin });
                    table.ForeignKey(
                        name: "FK_lista_item_revisao_lista_item_ItemId",
                        column: x => x.ItemId,
                        principalTable: "lista_item",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lista_item_LinkId",
                table: "lista_item",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_lista_item_LinkId_Fingerprint",
                table: "lista_item",
                columns: new[] { "LinkId", "Fingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lista_item_Tipo",
                table: "lista_item",
                column: "Tipo");

            migrationBuilder.CreateIndex(
                name: "IX_lista_link_ExcluidoEm",
                table: "lista_link",
                column: "ExcluidoEm");

            migrationBuilder.CreateIndex(
                name: "IX_lista_link_IdPlanilha",
                table: "lista_link",
                column: "IdPlanilha",
                unique: true,
                filter: "\"ExcluidoEm\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lista_item_revisao");

            migrationBuilder.DropTable(
                name: "lista_item");

            migrationBuilder.DropTable(
                name: "lista_link");
        }
    }
}
