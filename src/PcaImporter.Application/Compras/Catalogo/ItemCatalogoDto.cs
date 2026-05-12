namespace PcaImporter.Application.Compras.Catalogo;

public sealed record ItemCatalogoDto(
    int CodigoItem,
    int CodigoGrupo,
    string NomeGrupo,
    int CodigoClasse,
    string NomeClasse,
    int CodigoPdm,
    string NomePdm,
    string DescricaoItem,
    bool StatusItem,
    bool ItemSustentavel,
    string Tipo,
    DateTimeOffset? DataHoraAtualizacao,
    string CorpoBruto
);
