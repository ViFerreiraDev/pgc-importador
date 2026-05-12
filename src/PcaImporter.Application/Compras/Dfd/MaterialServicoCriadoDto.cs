namespace PcaImporter.Application.Compras.Dfd;

public sealed record MaterialServicoCriadoDto(
    long Id,
    long IdFormalizacaoDemanda,
    string Tipo,
    string Codigo,
    int IdClasse,
    string NomeClasse,
    int IdPadraoDescritivo,
    string NomePadraoDescritivo,
    decimal Quantidade,
    decimal ValorUnitario,
    decimal ValorTotal,
    string Moeda,
    string SiglaUnidadeFornecimento,
    string NomeUnidadeFornecimento,
    DateTimeOffset DataHoraOperacao,
    long LoginOperacao,
    string CorpoBruto
);
