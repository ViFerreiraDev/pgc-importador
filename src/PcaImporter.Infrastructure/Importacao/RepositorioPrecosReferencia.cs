using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using PcaImporter.Application.Importacao;

namespace PcaImporter.Infrastructure.Importacao;

/// <summary>
/// Repositório read-only que consulta o `comprasgov.db` (base de referência
/// derivada do histórico do Compras.gov.br).
///
/// Schemas suportados (detectados automaticamente):
///  • PREÇO: aceita <c>preco_min</c>/<c>preco_max</c> ou <c>preco_minimo</c>/<c>preco_maximo</c>.
///  • QUANTIDADE: aceita <c>qtd_min</c>/<c>qtd_max</c>; se não existirem, usa <c>qtd_media</c>
///    como min e como max (banda 50%/1.5× ainda funciona em volta da média).
///  • Se nem mesmo preço min/max existir, divergências ficam desabilitadas.
/// </summary>
public sealed class RepositorioPrecosReferencia : IRepositorioPrecosReferencia
{
    private readonly string _connStr;
    private readonly ILogger<RepositorioPrecosReferencia> _log;
    private readonly bool _disponivel;
    private readonly bool _schemaCompativel;
    private readonly string _colPrecoMin = "preco_min";
    private readonly string _colPrecoMax = "preco_max";
    private readonly string _colQtdMin = "qtd_min";
    private readonly string _colQtdMax = "qtd_max";

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
            _log.LogWarning("Base de referência {Caminho} não encontrada. Divergências desabilitadas.", caminhoDb);
            _schemaCompativel = false;
            return;
        }

        _schemaCompativel = DetectarSchemaRange(
            out _colPrecoMin, out _colPrecoMax, out _colQtdMin, out _colQtdMax);
        if (!_schemaCompativel)
        {
            _log.LogWarning(
                "Base de referência {Caminho} sem colunas preco_min/preco_max (ou preco_minimo/preco_maximo). " +
                "Divergências ficam desabilitadas até atualizar a base.", caminhoDb);
        }
        else
        {
            _log.LogInformation(
                "Base de referência {Caminho} OK · preço: {Pmin}/{Pmax} · quantidade: {Qmin}/{Qmax}",
                caminhoDb, _colPrecoMin, _colPrecoMax, _colQtdMin, _colQtdMax);
        }
    }

    private bool DetectarSchemaRange(out string precoMin, out string precoMax, out string qtdMin, out string qtdMax)
    {
        precoMin = precoMax = qtdMin = qtdMax = string.Empty;
        try
        {
            using var con = new SqliteConnection(_connStr);
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(item_preco);";
            using var r = cmd.ExecuteReader();
            var colunas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (r.Read())
            {
                colunas.Add(r.GetString(1));
            }

            // Preço: aceita os dois nomes
            if (colunas.Contains("preco_min")) precoMin = "preco_min";
            else if (colunas.Contains("preco_minimo")) precoMin = "preco_minimo";
            else return false;

            if (colunas.Contains("preco_max")) precoMax = "preco_max";
            else if (colunas.Contains("preco_maximo")) precoMax = "preco_maximo";
            else return false;

            // Quantidade: aceita min/max (várias grafias); se não, cai pra média (qtd_media como min e max)
            if (colunas.Contains("qtd_min")) qtdMin = "qtd_min";
            else if (colunas.Contains("qtd_minimo")) qtdMin = "qtd_minimo";
            else if (colunas.Contains("qtd_minima")) qtdMin = "qtd_minima";
            else if (colunas.Contains("qtd_media")) qtdMin = "qtd_media";
            else qtdMin = "NULL"; // sem qualquer referência de quantidade

            if (colunas.Contains("qtd_max")) qtdMax = "qtd_max";
            else if (colunas.Contains("qtd_maximo")) qtdMax = "qtd_maximo";
            else if (colunas.Contains("qtd_maxima")) qtdMax = "qtd_maxima";
            else if (colunas.Contains("qtd_media")) qtdMax = "qtd_media";
            else qtdMax = "NULL";

            return true;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Falha ao inspecionar schema de item_preco.");
            return false;
        }
    }

    public IReadOnlyList<PrecoReferenciaDto> BuscarPorCodigo(int codigoItemCatalogo)
    {
        if (!_disponivel || !_schemaCompativel) return Array.Empty<PrecoReferenciaDto>();
        try
        {
            using var con = new SqliteConnection(_connStr);
            con.Open();
            using var cmd = con.CreateCommand();
            // nomes de coluna foram validados via PRAGMA — interpolação segura
            cmd.CommandText = $@"
SELECT codigo_item_catalogo, descricao, sigla_unidade_fornecimento,
       {_colPrecoMin}, {_colPrecoMax}, {_colQtdMin}, {_colQtdMax},
       total_registros, anos_considerados
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
                    PrecoMin: reader.IsDBNull(3) ? 0m : (decimal)reader.GetDouble(3),
                    PrecoMax: reader.IsDBNull(4) ? 0m : (decimal)reader.GetDouble(4),
                    QuantidadeMin: reader.IsDBNull(5) ? 0m : (decimal)reader.GetDouble(5),
                    QuantidadeMax: reader.IsDBNull(6) ? 0m : (decimal)reader.GetDouble(6),
                    TotalRegistros: reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                    AnosConsiderados: reader.IsDBNull(8) ? 0 : reader.GetInt32(8)
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
