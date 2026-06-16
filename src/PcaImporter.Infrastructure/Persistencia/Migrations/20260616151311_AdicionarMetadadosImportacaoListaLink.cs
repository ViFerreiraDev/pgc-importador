using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PcaImporter.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMetadadosImportacaoListaLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descricao",
                table: "lista_link",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ImportadoEm",
                table: "lista_link",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimoErroImportacao",
                table: "lista_link",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UltimoIdExecucao",
                table: "lista_link",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descricao",
                table: "lista_link");

            migrationBuilder.DropColumn(
                name: "ImportadoEm",
                table: "lista_link");

            migrationBuilder.DropColumn(
                name: "UltimoErroImportacao",
                table: "lista_link");

            migrationBuilder.DropColumn(
                name: "UltimoIdExecucao",
                table: "lista_link");
        }
    }
}
