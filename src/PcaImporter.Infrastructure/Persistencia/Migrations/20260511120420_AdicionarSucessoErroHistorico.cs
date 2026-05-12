using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcaImporter.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarSucessoErroHistorico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinhaErro",
                table: "historico_importacoes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MensagemErro",
                table: "historico_importacoes",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Sucesso",
                table: "historico_importacoes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            // Backfill: registros existentes só eram salvos em caso de sucesso.
            migrationBuilder.Sql("UPDATE historico_importacoes SET Sucesso = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinhaErro",
                table: "historico_importacoes");

            migrationBuilder.DropColumn(
                name: "MensagemErro",
                table: "historico_importacoes");

            migrationBuilder.DropColumn(
                name: "Sucesso",
                table: "historico_importacoes");
        }
    }
}
