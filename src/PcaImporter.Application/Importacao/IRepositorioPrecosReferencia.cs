namespace PcaImporter.Application.Importacao;

/// <summary>
/// Referência de preço/quantidade de um item do catálogo CATMAT, no formato range
/// (min/max), calculada a partir do histórico do Compras.gov.br.
/// </summary>
public sealed record PrecoReferenciaDto(
    int CodigoItemCatalogo,
    string Descricao,
    string SiglaUnidadeFornecimento,
    decimal PrecoMin,
    decimal PrecoMax,
    decimal QuantidadeMin,
    decimal QuantidadeMax,
    int TotalRegistros,
    int AnosConsiderados
);

public interface IRepositorioPrecosReferencia
{
    /// <summary>
    /// Retorna todas as variantes (uma por sigla de unidade) cadastradas para o código.
    /// Lista vazia se não houver dados ou se o schema do banco for incompatível.
    /// </summary>
    IReadOnlyList<PrecoReferenciaDto> BuscarPorCodigo(int codigoItemCatalogo);
}
