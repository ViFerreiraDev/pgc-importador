using ClosedXML.Excel;
using PcaImporter.Application.Importacao;

namespace PcaImporter.Infrastructure.Importacao;

public static class LeitorPlanilhaDfd
{
    public const string Aba_Dfd = "DFD";
    public const string Aba_Materiais = "Materiais";

    public static (EntradaImportacaoDfd? Entrada, List<ErroValidacao> Erros) Ler(Stream xlsx)
    {
        var erros = new List<ErroValidacao>();
        XLWorkbook wb;
        try
        {
            wb = new XLWorkbook(xlsx);
        }
        catch (Exception ex)
        {
            erros.Add(new ErroValidacao("arquivo", "geral", $"Não foi possível abrir o XLSX: {ex.Message}"));
            return (null, erros);
        }

        if (!wb.TryGetWorksheet(Aba_Dfd, out var abaDfd))
        {
            erros.Add(new ErroValidacao("arquivo", "abaDfd", $"Aba '{Aba_Dfd}' não encontrada."));
        }
        if (!wb.TryGetWorksheet(Aba_Materiais, out var abaMat))
        {
            erros.Add(new ErroValidacao("arquivo", "abaMateriais", $"Aba '{Aba_Materiais}' não encontrada."));
        }
        if (abaDfd is null || abaMat is null)
        {
            return (null, erros);
        }

        var entrada = new EntradaImportacaoDfd();

        // Aba DFD: cabecalho na linha 1, valores na linha 2
        var mapaDfd = LerCabecalhoMapa(abaDfd, esperadas:
        [
            "dataConclusaoContratacao", "descricao", "nivelPrioridade",
            "justificativaPrioridade", "justificativaNecessidade",
            "responsavelCpf", "responsavelNome", "responsavelEmail", "responsavelCargo"
        ], localCabecalho: "DFD", erros);

        if (mapaDfd is not null)
        {
            var l2 = abaDfd.Row(2);
            entrada.DataConclusaoContratacao = LerData(l2, mapaDfd, "dataConclusaoContratacao");
            entrada.DataConclusaoContratacaoTexto = LerString(l2, mapaDfd, "dataConclusaoContratacao");
            entrada.Descricao = LerString(l2, mapaDfd, "descricao");
            entrada.NivelPrioridadeTexto = LerString(l2, mapaDfd, "nivelPrioridade");
            entrada.JustificativaPrioridade = LerString(l2, mapaDfd, "justificativaPrioridade");
            entrada.JustificativaNecessidade = LerString(l2, mapaDfd, "justificativaNecessidade");
            entrada.ResponsavelCpf = LerString(l2, mapaDfd, "responsavelCpf");
            entrada.ResponsavelNome = LerString(l2, mapaDfd, "responsavelNome");
            entrada.ResponsavelEmail = LerString(l2, mapaDfd, "responsavelEmail");
            entrada.ResponsavelCargo = LerString(l2, mapaDfd, "responsavelCargo");
        }

        // Aba Materiais: cabecalho linha 1, dados a partir da linha 2.
        // Tipo (MATERIAL) e Moeda (Real) sao fixos e nao saem mais na planilha.
        // Ordem recomendada das colunas (nao importa para a leitura — mapeamos por NOME):
        //   codigo · descritivo (opcional, ignorado) · siglaUnidadeFornecimento · quantidade · valorUnitario
        // Qualquer coluna extra (ex.: 'descritivo' para conferencia visual) eh aceita e
        // simplesmente ignorada — nenhum aviso eh gerado.
        var mapaMat = LerCabecalhoMapa(abaMat, esperadas:
        [
            "codigo", "quantidade", "valorUnitario", "siglaUnidadeFornecimento"
        ], localCabecalho: "Materiais", erros);

        if (mapaMat is not null)
        {
            var ultimaLinha = abaMat.LastRowUsed()?.RowNumber() ?? 1;
            for (var linha = 2; linha <= ultimaLinha; linha++)
            {
                var row = abaMat.Row(linha);
                if (row.IsEmpty()) continue;

                var mat = new EntradaImportacaoMaterial
                {
                    LinhaPlanilha = linha,
                    Codigo = LerString(row, mapaMat, "codigo"),
                    Tipo = "MATERIAL",
                    Quantidade = LerDecimal(row, mapaMat, "quantidade"),
                    QuantidadeTexto = LerString(row, mapaMat, "quantidade"),
                    ValorUnitario = LerDecimal(row, mapaMat, "valorUnitario"),
                    ValorUnitarioTexto = LerString(row, mapaMat, "valorUnitario"),
                    SiglaUnidadeFornecimento = LerString(row, mapaMat, "siglaUnidadeFornecimento"),
                    Moeda = "Real",
                };
                entrada.Materiais.Add(mat);
            }
        }

        return (entrada, erros);
    }

    private static Dictionary<string, int>? LerCabecalhoMapa(IXLWorksheet aba, string[] esperadas, string localCabecalho, List<ErroValidacao> erros)
    {
        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var linha1 = aba.Row(1);
        var ultimaCol = aba.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var c = 1; c <= ultimaCol; c++)
        {
            var nome = linha1.Cell(c).GetString().Trim();
            if (!string.IsNullOrEmpty(nome) && !mapa.ContainsKey(nome))
            {
                mapa[nome] = c;
            }
        }

        var ausentes = esperadas.Where(e => !mapa.ContainsKey(e)).ToList();
        foreach (var a in ausentes)
        {
            erros.Add(new ErroValidacao(localCabecalho, a, $"Coluna obrigatória ausente: '{a}'"));
        }
        return ausentes.Count == 0 ? mapa : null;
    }

    private static string? LerString(IXLRow row, Dictionary<string, int> mapa, string col)
    {
        if (!mapa.TryGetValue(col, out var idx)) return null;
        var s = row.Cell(idx).GetString().Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static decimal? LerDecimal(IXLRow row, Dictionary<string, int> mapa, string col)
    {
        if (!mapa.TryGetValue(col, out var idx)) return null;
        var cell = row.Cell(idx);
        if (cell.IsEmpty()) return null;

        // Sempre prioriza o valor FORMATADO da celula (o que o usuario enxerga).
        // Motivos:
        //  - pt-BR + texto "0.11" via TryGetValue vira 11 (ponto vira separador de milhar).
        //  - Google Sheets exporta moeda em "micros" (1 R$ = 1.000.000) no .xlsx — o
        //    raw fica gigante, mas o formatado mostra o valor real ("R$ 12,00").
        //  - Celulas com prefixo "R$" so existem no formatado, nao no raw.
        var formatado = cell.GetFormattedString();
        var doFormatado = ParsearDecimalLivre(formatado);
        if (doFormatado.HasValue) return doFormatado;

        // Fallback: raw nativo (so quando formatado nao parsear).
        if (cell.DataType == XLDataType.Number && cell.TryGetValue<decimal>(out var d))
        {
            return d;
        }
        return ParsearDecimalLivre(cell.GetString());
    }

    /// <summary>
    /// Aceita números em variantes brasileiras e americanas, com ou sem prefixo monetário:
    ///   "12", "12.5", "12,5", "1.234,56", "1,234.56", "R$ 12,00", "R$12.00", "$ 1.000", "12,00 "
    /// Regra: tira tudo que não é dígito/sinal/separador. Decide o separador decimal pela
    /// última vírgula ou ponto encontrado; o que sobrar é separador de milhar e é descartado.
    /// </summary>
    internal static decimal? ParsearDecimalLivre(string? entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada)) return null;

        // Normaliza: tira espaços (incl. NBSP), R$, US$, símbolos comuns
        var sb = new System.Text.StringBuilder(entrada.Length);
        foreach (var ch in entrada)
        {
            if (char.IsDigit(ch) || ch == ',' || ch == '.' || ch == '-' || ch == '+')
            {
                sb.Append(ch);
            }
        }
        var limpo = sb.ToString();
        if (limpo.Length == 0) return null;

        var ultimaVirgula = limpo.LastIndexOf(',');
        var ultimoPonto = limpo.LastIndexOf('.');

        string normalizado;
        if (ultimaVirgula < 0 && ultimoPonto < 0)
        {
            normalizado = limpo;
        }
        else if (ultimaVirgula > ultimoPonto)
        {
            // vírgula é decimal (formato BR). Pontos viram milhar e são descartados.
            normalizado = limpo.Replace(".", string.Empty).Replace(',', '.');
        }
        else
        {
            // ponto é decimal (formato US). Vírgulas são milhar e descartadas.
            normalizado = limpo.Replace(",", string.Empty);
        }

        return decimal.TryParse(normalizado,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var p) ? p : null;
    }

    private static DateOnly? LerData(IXLRow row, Dictionary<string, int> mapa, string col)
    {
        if (!mapa.TryGetValue(col, out var idx)) return null;
        var cell = row.Cell(idx);
        if (cell.IsEmpty()) return null;
        if (cell.TryGetValue<DateTime>(out var dt)) return DateOnly.FromDateTime(dt);
        var s = cell.GetString().Trim();
        if (DateOnly.TryParse(s, out var d)) return d;
        if (DateTime.TryParse(s, out var dt2)) return DateOnly.FromDateTime(dt2);
        return null;
    }

}
