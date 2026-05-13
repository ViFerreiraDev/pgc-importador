using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PcaImporter.Application.Importacao;

namespace PcaImporter.Infrastructure.Importacao;

/// <summary>
/// Repositório read-only que consulta o `comprasgov.db` (base de referência de
/// preços/quantitativos derivada do histórico do Compras.gov.br).
/// Singleton — abre conexão sob demanda. SQLite suporta múltiplos leitores.
/// </summary>
public sealed class RepositorioPrecosReferencia : IRepositorioPrecosReferencia
{
    private readonly string _connStr;
    private readonly ILogger<RepositorioPrecosReferencia> _log;
    private readonly bool _disponivel;

    public RepositorioPrecosReferencia(string caminhoDb, ILogger<RepositorioPrecosReferencia> log)
    {
        _log = log;
        _connStr = new SqliteConnectionStringBuilder
        {
            DataSource = caminhoDb,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
        _disponivel = File.Exists(caminhoDb);
        if (!_disponivel)
        {
            _log.LogWarning("Base de referência {Caminho} não encontrada. Validação de preço/qtd ficará desabilitada.", caminhoDb);
        }
    }

    public IReadOnlyList<PrecoReferenciaDto> BuscarPorCodigo(int codigoItemCatalogo)
    {
        if (!_disponivel) return Array.Empty<PrecoReferenciaDto>();
        try
        {
            using var con = new SqliteConnection(_connStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = @"
SELECT codigo_item_catalogo, descricao, sigla_unidade_fornecimento,
       preco_medio, qtd_media, total_registros, anos_considerados
FROM item_preco
WHERE codigo_item_catalogo = $codigo;";
            cmd.Parameters.AddWithValue("$codigo", codigoItemCatalogo);

            var lista = new List<PrecoReferenciaDto>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new PrecoReferenciaDto(
                    CodigoItemCatalogo: reader.GetInt32(0),
                    Descricao: reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    SiglaUnidadeFornecimento: reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    PrecoMedio: reader.IsDBNull(3) ? 0m : (decimal)reader.GetDouble(3),
                    QuantidadeMedia: reader.IsDBNull(4) ? 0m : (decimal)reader.GetDouble(4),
                    TotalRegistros: reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
                    AnosConsiderados: reader.IsDBNull(6) ? 0 : reader.GetInt32(6)
                ));
            }
            return lista;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao consultar referência do código {Codigo}", codigoItemCatalogo);
            return Array.Empty<PrecoReferenciaDto>();
        }
    }
}
