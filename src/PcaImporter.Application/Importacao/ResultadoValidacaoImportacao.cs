namespace PcaImporter.Application.Importacao;

public sealed record ResultadoValidacaoImportacao(
    bool Valido,
    int TotalMateriais,
    IReadOnlyList<ErroValidacao> Erros,
    IReadOnlyList<AvisoValidacao> Avisos,
    IReadOnlyList<DivergenciaValidacao> Divergencias,
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

/// <summary>
/// Divergência informativa entre o valor da planilha e a referência histórica do
/// Compras.gov.br. Não bloqueia a importação — é só um sinal pra revisão humana
/// quando a diferença for considerável.
/// </summary>
public sealed record DivergenciaValidacao(
    string Local,
    int? Linha,
    string Codigo,
    /// <summary>"preco" ou "qtd"</summary>
    string Tipo,
    decimal ValorPlanilha,
    decimal ValorReferencia,
    /// <summary>Diferença relativa (0.5 = 50% acima/abaixo).</summary>
    decimal DiferencaPct,
    string SiglaReferencia,
    int TotalRegistros,
    string Mensagem
);
