using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcaImporter.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarClasseGrupoListaLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Classe",
                table: "lista_link",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroGrupo",
                table: "lista_link",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_lista_link_Classe_NumeroGrupo",
                table: "lista_link",
                columns: new[] { "Classe", "NumeroGrupo" },
                unique: true,
                filter: "\"ExcluidoEm\" IS NULL AND \"Classe\" IS NOT NULL AND \"NumeroGrupo\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_lista_link_Classe_NumeroGrupo",
                table: "lista_link");

            migrationBuilder.DropColumn(
                name: "Classe",
                table: "lista_link");

            migrationBuilder.DropColumn(
                name: "NumeroGrupo",
                table: "lista_link");
        }
    }
}
