using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PcaImporter.Infrastructure.Persistencia;

namespace PcaImporter.Infrastructure.Tests.Persistencia;

public sealed class RegistrarExclusaoDfdHistoricoMigrationTests
{
    [Fact]
    public async Task MigracaoRetroativa_InvalidaImportacaoAnteriorSemAfetarReimportacaoPosterior()
    {
        await using var conexao = new SqliteConnection("Data Source=:memory:");
        await conexao.OpenAsync();
        var opcoes = new DbContextOptionsBuilder<PcaDbContext>()
            .UseSqlite(conexao)
            .Options;

        await using var ctx = new PcaDbContext(opcoes);
        var migrador = ctx.GetService<IMigrator>();
        await migrador.MigrateAsync("20260616151311_AdicionarMetadadosImportacaoListaLink");

        await ExecutarAsync(conexao,
            """
            INSERT INTO "historico_importacoes"
                ("Id", "IdPlanilha", "UrlOriginal", "ImportadaEm", "IdExecucao",
                 "NumeroDfd", "AnoDfd", "IdArtefato", "IdFormalizacaoDemanda",
                 "TotalMateriais", "ValorTotal", "Sucesso")
            VALUES
                (1, 'planilha-1', 'url', '2026-08-19 10:00:00+00:00', 'exec-1',
                 1, 2026, 101, 1001, 1, '10', 1),
                (2, 'planilha-1', 'url', '2026-08-19 14:00:00+00:00', 'exec-2',
                 2, 2026, 202, 2002, 1, '20', 1);

            INSERT INTO "lista_link"
                ("Id", "Url", "IdPlanilha", "Estado", "CriadoEm", "ImportadoEm",
                 "ExcluidoEm", "ExcluidoPorLogin")
            VALUES
                (1, 'url', 'planilha-1', 'valido', '2026-08-19 09:00:00+00:00',
                 '2026-08-19 10:00:00+00:00', '2026-08-19 12:00:00+00:00', 'usuario.teste'),
                (2, 'url', 'planilha-1', 'valido', '2026-08-19 13:00:00+00:00',
                 '2026-08-19 14:00:00+00:00', NULL, NULL);
            """);

        await migrador.MigrateAsync();

        await using var consulta = conexao.CreateCommand();
        consulta.CommandText =
            "SELECT \"Id\", \"DfdExcluidoEm\" FROM \"historico_importacoes\" ORDER BY \"Id\"";
        await using var leitor = await consulta.ExecuteReaderAsync();

        Assert.True(await leitor.ReadAsync());
        Assert.Equal(1, leitor.GetInt32(0));
        Assert.False(leitor.IsDBNull(1));

        Assert.True(await leitor.ReadAsync());
        Assert.Equal(2, leitor.GetInt32(0));
        Assert.True(leitor.IsDBNull(1));
    }

    private static async Task ExecutarAsync(SqliteConnection conexao, string sql)
    {
        await using var comando = conexao.CreateCommand();
        comando.CommandText = sql;
        await comando.ExecuteNonQueryAsync();
    }
}
