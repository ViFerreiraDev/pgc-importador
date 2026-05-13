namespace PcaImporter.Application.Importacao;

/// <summary>
/// Referência de preço/quantidade média de um item do catálogo CATMAT,
/// calculada a partir do histórico do Compras.gov.br.
/// </summary>
public sealed record PrecoReferenciaDto(
    int CodigoItemCatalogo,
    string Descricao,
    string SiglaUnidadeFornecimento,
    decimal PrecoMedio,
    decimal QuantidadeMedia,
    int TotalRegistros,
    int AnosConsiderados
);

public interface IRepositorioPrecosReferencia
{
    /// <summary>
    /// Retorna todas as variantes (uma por sigla de unidade) cadastradas para o código.
    /// Lista vazia se não houver dados.
    /// </summary>
    IReadOnlyList<PrecoReferenciaDto> BuscarPorCodigo(int codigoItemCatalogo);
}
