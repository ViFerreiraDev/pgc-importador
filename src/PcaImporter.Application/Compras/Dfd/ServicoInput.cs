namespace PcaImporter.Application.Compras.Dfd;

/// <summary>
/// Payload exigido pelo Compras.gov para incluir um item CATSER no DFD.
/// Serviços usam grupo e unidade de medida, enquanto materiais usam classe,
/// padrão descritivo e unidade de fornecimento.
/// </summary>
public sealed record ServicoInput(
    string Codigo,
    string Tipo,
    string SiglaUnidadeMedida,
    string Descricao,
    decimal ValorUnitario,
    string Moeda,
    string NomeGrupo,
    decimal Quantidade,
    long IdFormalizacaoDemanda,
    int IdGrupo
);
