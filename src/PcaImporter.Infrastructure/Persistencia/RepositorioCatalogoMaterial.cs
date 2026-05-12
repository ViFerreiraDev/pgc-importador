using Microsoft.EntityFrameworkCore;
using PcaImporter.Application.Catalogo;
using PcaImporter.Infrastructure.Persistencia.Entidades;

namespace PcaImporter.Infrastructure.Persistencia;

public sealed class RepositorioCatalogoMaterial : IRepositorioCatalogoMaterial
{
    private readonly IDbContextFactory<PcaDbContext> _factory;

    public RepositorioCatalogoMaterial(IDbContextFactory<PcaDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<int> ContarAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.CatalogoMateriais.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<DateTimeOffset?> UltimaSincronizacaoAsync(CancellationToken ct = default)
    {
        // SQLite nao traduz Max/OrderBy em DateTimeOffset. Como gravamos sempre em UTC ISO-8601
        // ("yyyy-MM-ddTHH:mm:ss.fffffff+00:00"), ordem lexicografica == ordem cronologica.
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct).ConfigureAwait(false);
        }
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(SincronizadoEm) FROM catalogo_materiais";
        var resultado = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        if (resultado is null || resultado is DBNull) return null;
        var texto = resultado.ToString();
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return DateTimeOffset.TryParse(texto, out var dto) ? dto : null;
    }

    public async Task<ItemCatalogoLocalDto?> BuscarPorCodigoAsync(int codigoItem, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var e = await ctx.CatalogoMateriais.AsNoTracking()
            .FirstOrDefaultAsync(x => x.CodigoItem == codigoItem, ct).ConfigureAwait(false);
        return e is null ? null : Mapear(e);
    }

    public async Task UpsertEmLoteAsync(IReadOnlyCollection<ItemCatalogoLocalDto> itens, CancellationToken ct = default)
    {
        if (itens.Count == 0) return;

        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var conn = ctx.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct).ConfigureAwait(false);
        }

        await using var tx = await conn.BeginTransactionAsync(ct).ConfigureAwait(false);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO catalogo_materiais
(CodigoItem, CodigoGrupo, NomeGrupo, CodigoClasse, NomeClasse, CodigoPdm, NomePdm, DescricaoItem, StatusItem, ItemSustentavel, CodigoNcm, AtualizadoEmFonte, SincronizadoEm)
VALUES ($CodigoItem, $CodigoGrupo, $NomeGrupo, $CodigoClasse, $NomeClasse, $CodigoPdm, $NomePdm, $DescricaoItem, $StatusItem, $ItemSustentavel, $CodigoNcm, $AtualizadoEmFonte, $SincronizadoEm)
ON CONFLICT(CodigoItem) DO UPDATE SET
  CodigoGrupo=excluded.CodigoGrupo,
  NomeGrupo=excluded.NomeGrupo,
  CodigoClasse=excluded.CodigoClasse,
  NomeClasse=excluded.NomeClasse,
  CodigoPdm=excluded.CodigoPdm,
  NomePdm=excluded.NomePdm,
  DescricaoItem=excluded.DescricaoItem,
  StatusItem=excluded.StatusItem,
  ItemSustentavel=excluded.ItemSustentavel,
  CodigoNcm=excluded.CodigoNcm,
  AtualizadoEmFonte=excluded.AtualizadoEmFonte,
  SincronizadoEm=excluded.SincronizadoEm";

            string[] nomes = ["$CodigoItem","$CodigoGrupo","$NomeGrupo","$CodigoClasse","$NomeClasse","$CodigoPdm","$NomePdm","$DescricaoItem","$StatusItem","$ItemSustentavel","$CodigoNcm","$AtualizadoEmFonte","$SincronizadoEm"];
            var parametros = new System.Data.Common.DbParameter[nomes.Length];
            for (var i = 0; i < nomes.Length; i++)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = nomes[i];
                cmd.Parameters.Add(p);
                parametros[i] = p;
            }

            foreach (var it in itens)
            {
                parametros[0].Value = it.CodigoItem;
                parametros[1].Value = it.CodigoGrupo;
                parametros[2].Value = it.NomeGrupo;
                parametros[3].Value = it.CodigoClasse;
                parametros[4].Value = it.NomeClasse;
                parametros[5].Value = it.CodigoPdm;
                parametros[6].Value = it.NomePdm;
                parametros[7].Value = it.DescricaoItem;
                parametros[8].Value = it.StatusItem ? 1 : 0;
                parametros[9].Value = it.ItemSustentavel ? 1 : 0;
                parametros[10].Value = (object?)it.CodigoNcm ?? DBNull.Value;
                parametros[11].Value = it.AtualizadoEmFonte.HasValue ? it.AtualizadoEmFonte.Value.ToString("o") : DBNull.Value;
                parametros[12].Value = it.SincronizadoEm.ToString("o");
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
        }
        await tx.CommitAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> LimparAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.CatalogoMateriais.ExecuteDeleteAsync(ct).ConfigureAwait(false);
    }

    private static ItemCatalogoLocalDto Mapear(CatalogoMaterialEntity e) =>
        new(e.CodigoItem, e.CodigoGrupo, e.NomeGrupo, e.CodigoClasse, e.NomeClasse,
            e.CodigoPdm, e.NomePdm, e.DescricaoItem, e.StatusItem, e.ItemSustentavel,
            e.CodigoNcm, e.AtualizadoEmFonte, e.SincronizadoEm);
}
