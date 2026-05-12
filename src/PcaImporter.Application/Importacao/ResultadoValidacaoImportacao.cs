namespace PcaImporter.Application.Importacao;

public sealed record ResultadoValidacaoImportacao(
    bool Valido,
    int TotalMateriais,
    IReadOnlyList<ErroValidacao> Erros,
    IReadOnlyList<AvisoValidacao> Avisos,
    EntradaImportacaoDfd? Entrada
);

public sealed record ErroValidacao(
    string Local,
    string Campo,
    string Mensagem,
    int? Linha = null
);

public sealed record AvisoValidacao(
    string Local,
    string Mensagem,
    int? Linha = null
);
