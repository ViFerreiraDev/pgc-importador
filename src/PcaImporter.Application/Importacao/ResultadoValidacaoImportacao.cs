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
/// Divergência informativa entre o valor da planilha e a faixa histórica do
/// Compras.gov.br (preço mínimo/máximo + margem 50%). Não bloqueia a importação —
/// é só um sinal pra revisão humana quando o valor está fora da banda esperada.
/// </summary>
public sealed record DivergenciaValidacao(
    string Local,
    int? Linha,
    string Codigo,
    /// <summary>"preco" ou "qtd"</summary>
    string Tipo,
    decimal ValorPlanilha,
    /// <summary>Limite inferior da banda histórica (min × 0.5).</summary>
    decimal ReferenciaMin,
    /// <summary>Limite superior da banda histórica (max × 1.5).</summary>
    decimal ReferenciaMax,
    /// <summary>Diferença relativa em relação ao limite ultrapassado (positivo = acima do max, negativo = abaixo do min).</summary>
    decimal DiferencaPct,
    string SiglaReferencia,
    int TotalRegistros,
    string Mensagem
);
