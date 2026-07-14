namespace PcaImporter.Application.Compras.Dfd;

public sealed record MaterialServicoInput(
    long IdFormalizacaoDemanda,
    string Tipo,
    string Codigo,
    int IdClasse,
    string NomeClasse,
    // Serviços (CATSER) não têm PDM — nulos são omitidos do JSON enviado.
    int? IdPadraoDescritivo,
    string? NomePadraoDescritivo,
    string Descricao,
    decimal Quantidade,
    decimal ValorUnitario,
    string Moeda,
    string SiglaUnidadeFornecimento
);
