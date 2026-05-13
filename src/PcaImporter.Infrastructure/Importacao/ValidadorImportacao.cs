using PcaImporter.Application.Importacao;

namespace PcaImporter.Infrastructure.Importacao;

public static class ValidadorImportacao
{
    /// <summary>Acima desse limiar (100% = preço dobrou ou caiu pela metade) emite divergência.</summary>
    private const decimal LimiarPrecoPct = 1.00m;

    /// <summary>Acima desse limiar (500% = qtd multiplicou por 6 ou ficou 1/6) emite divergência.</summary>
    private const decimal LimiarQuantidadePct = 5.00m;

    /// <summary>Ignora referências com poucas observações — ruído estatístico.</summary>
    private const int MinRegistrosReferencia = 3;

    public static (List<ErroValidacao> Erros, List<AvisoValidacao> Avisos, List<DivergenciaValidacao> Divergencias) Validar(
        EntradaImportacaoDfd entrada,
        IRepositorioPrecosReferencia? referencias = null)
    {
        var erros = new List<ErroValidacao>();
        var avisos = new List<AvisoValidacao>();
        var divergencias = new List<DivergenciaValidacao>();

        // ----- Aba DFD -----

        // Data
        if (string.IsNullOrWhiteSpace(entrada.DataConclusaoContratacaoTexto))
        {
            erros.Add(new ErroValidacao("DFD", "dataConclusaoContratacao", "Data da conclusão da contratação é obrigatória."));
        }
        else if (entrada.DataConclusaoContratacao is null)
        {
            erros.Add(new ErroValidacao("DFD", "dataConclusaoContratacao",
                $"Data inválida '{entrada.DataConclusaoContratacaoTexto}'. Aceitos: AAAA-MM-DD (ex.: 2027-05-15) ou DD/MM/AAAA (ex.: 15/05/2027)."));
        }
        else if (entrada.DataConclusaoContratacao.Value < DateOnly.FromDateTime(DateTime.Today))
        {
            avisos.Add(new AvisoValidacao("DFD", $"Data {entrada.DataConclusaoContratacao:yyyy-MM-dd} está no passado."));
        }

        // Descrição
        if (string.IsNullOrWhiteSpace(entrada.Descricao))
        {
            erros.Add(new ErroValidacao("DFD", "descricao", "Descrição é obrigatória."));
        }
        else if (entrada.Descricao.Length > 200)
        {
            erros.Add(new ErroValidacao("DFD", "descricao", $"Descrição com {entrada.Descricao.Length} caracteres (máx 200)."));
        }

        // Prioridade: normaliza in-place
        var (prio, erroPrio) = Validadores.NormalizarPrioridade(entrada.NivelPrioridadeTexto);
        if (prio is not null)
        {
            entrada.NivelPrioridade = prio.Value;
            // se houve correção (ex: "Alta" → "ALTO"), avisa
            var canonico = prio.Value.ToString();
            if (!string.Equals(entrada.NivelPrioridadeTexto?.Trim(), canonico, StringComparison.Ordinal))
            {
                avisos.Add(new AvisoValidacao("DFD",
                    $"Prioridade '{entrada.NivelPrioridadeTexto}' normalizada para '{canonico}'."));
            }
        }
        else if (erroPrio is not null)
        {
            erros.Add(new ErroValidacao("DFD", "nivelPrioridade", erroPrio));
        }

        // Justificativa de prioridade obrigatoria quando ALTO
        if (prio == Application.Compras.Dfd.NivelPrioridade.ALTO && string.IsNullOrWhiteSpace(entrada.JustificativaPrioridade))
        {
            erros.Add(new ErroValidacao("DFD", "justificativaPrioridade",
                "Justificativa de prioridade é obrigatória quando prioridade é ALTO."));
        }

        // Justificativa de necessidade
        if (string.IsNullOrWhiteSpace(entrada.JustificativaNecessidade))
        {
            erros.Add(new ErroValidacao("DFD", "justificativaNecessidade", "Justificativa de necessidade é obrigatória."));
        }

        // Responsavel
        if (!Validadores.EhCpfValido(entrada.ResponsavelCpf))
        {
            erros.Add(new ErroValidacao("DFD", "responsavelCpf",
                "CPF do responsável inválido (verifique os dígitos verificadores)."));
        }
        if (string.IsNullOrWhiteSpace(entrada.ResponsavelNome))
        {
            erros.Add(new ErroValidacao("DFD", "responsavelNome", "Nome do responsável é obrigatório."));
        }
        if (string.IsNullOrWhiteSpace(entrada.ResponsavelEmail) ||
            !System.Text.RegularExpressions.Regex.IsMatch(entrada.ResponsavelEmail ?? string.Empty, @".+@.+\..+"))
        {
            erros.Add(new ErroValidacao("DFD", "responsavelEmail", "Email do responsável inválido."));
        }
        if (string.IsNullOrWhiteSpace(entrada.ResponsavelCargo))
        {
            erros.Add(new ErroValidacao("DFD", "responsavelCargo", "Cargo do responsável é obrigatório."));
        }

        // ----- Materiais -----
        if (entrada.Materiais.Count == 0)
        {
            erros.Add(new ErroValidacao("Materiais", "(geral)", "Nenhum material informado. Pelo menos 1 linha é necessária."));
        }
        else
        {
            var codigosVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in entrada.Materiais)
            {
                // Codigo: 6 dígitos
                if (string.IsNullOrWhiteSpace(m.Codigo))
                {
                    erros.Add(new ErroValidacao("Materiais", "codigo", "Código do item é obrigatório.", m.LinhaPlanilha));
                }
                else if (!Validadores.EhCodigoBrValido(m.Codigo))
                {
                    erros.Add(new ErroValidacao("Materiais", "codigo",
                        $"Código '{m.Codigo}' inválido. Deve ter exatamente 6 dígitos numéricos.", m.LinhaPlanilha));
                }
                else if (!codigosVistos.Add(m.Codigo.Trim()))
                {
                    avisos.Add(new AvisoValidacao("Materiais",
                        $"Código '{m.Codigo}' aparece mais de uma vez.", m.LinhaPlanilha));
                }

                // Quantidade
                if (string.IsNullOrWhiteSpace(m.QuantidadeTexto))
                {
                    erros.Add(new ErroValidacao("Materiais", "quantidade", "Quantidade é obrigatória.", m.LinhaPlanilha));
                }
                else if (m.Quantidade is null)
                {
                    erros.Add(new ErroValidacao("Materiais", "quantidade",
                        $"Quantidade '{m.QuantidadeTexto}' não é um número válido.", m.LinhaPlanilha));
                }
                else if (m.Quantidade <= 0)
                {
                    erros.Add(new ErroValidacao("Materiais", "quantidade",
                        "Quantidade deve ser maior que zero.", m.LinhaPlanilha));
                }

                // Valor unitario
                if (string.IsNullOrWhiteSpace(m.ValorUnitarioTexto))
                {
                    erros.Add(new ErroValidacao("Materiais", "valorUnitario", "Valor unitário é obrigatório.", m.LinhaPlanilha));
                }
                else if (m.ValorUnitario is null)
                {
                    erros.Add(new ErroValidacao("Materiais", "valorUnitario",
                        $"Valor unitário '{m.ValorUnitarioTexto}' não é um número válido.", m.LinhaPlanilha));
                }
                else if (m.ValorUnitario <= 0)
                {
                    erros.Add(new ErroValidacao("Materiais", "valorUnitario",
                        "Valor unitário deve ser maior que zero.", m.LinhaPlanilha));
                }

                // Sigla
                if (string.IsNullOrWhiteSpace(m.SiglaUnidadeFornecimento))
                {
                    erros.Add(new ErroValidacao("Materiais", "siglaUnidadeFornecimento",
                        "Sigla da unidade é obrigatória (ex: UN).", m.LinhaPlanilha));
                }

                // Divergência de preço/quantidade contra a base de referência (não bloqueia)
                if (referencias is not null
                    && m.Quantidade is { } qtd && qtd > 0
                    && m.ValorUnitario is { } valor && valor > 0
                    && int.TryParse(m.Codigo, out var codigoInt))
                {
                    AnalisarDivergencias(referencias, codigoInt, m.SiglaUnidadeFornecimento, qtd, valor, m.LinhaPlanilha, divergencias);
                }
            }
        }

        return (erros, avisos, divergencias);
    }

    private static void AnalisarDivergencias(
        IRepositorioPrecosReferencia repo,
        int codigo,
        string? sigla,
        decimal quantidade,
        decimal valorUnitario,
        int? linha,
        List<DivergenciaValidacao> divergencias)
    {
        var refs = repo.BuscarPorCodigo(codigo);
        if (refs.Count == 0) return;

        // Filtra por significância estatística
        var elegiveis = refs.Where(r => r.TotalRegistros >= MinRegistrosReferencia && r.PrecoMedio > 0).ToList();
        if (elegiveis.Count == 0) return;

        // Escolhe a referência mais adequada:
        //   1) mesma sigla de unidade, se houver;
        //   2) senão, a que tiver o preço mais próximo do informado (regra do usuário).
        PrecoReferenciaDto? escolhida = null;
        if (!string.IsNullOrWhiteSpace(sigla))
        {
            escolhida = elegiveis.FirstOrDefault(r =>
                string.Equals(r.SiglaUnidadeFornecimento, sigla, StringComparison.OrdinalIgnoreCase));
        }
        if (escolhida is null)
        {
            escolhida = elegiveis.OrderBy(r => Math.Abs(r.PrecoMedio - valorUnitario)).First();
        }

        // Diferença percentual do preço unitário
        var diffPreco = Math.Abs(valorUnitario - escolhida.PrecoMedio) / escolhida.PrecoMedio;
        if (diffPreco >= LimiarPrecoPct)
        {
            var direcao = valorUnitario > escolhida.PrecoMedio ? "acima" : "abaixo";
            divergencias.Add(new DivergenciaValidacao(
                Local: "Materiais",
                Linha: linha,
                Codigo: codigo.ToString(),
                Tipo: "preco",
                ValorPlanilha: valorUnitario,
                ValorReferencia: escolhida.PrecoMedio,
                DiferencaPct: diffPreco,
                SiglaReferencia: escolhida.SiglaUnidadeFornecimento,
                TotalRegistros: escolhida.TotalRegistros,
                Mensagem:
                    $"Valor unitário R$ {valorUnitario:N2} está {diffPreco:P0} {direcao} da média histórica " +
                    $"R$ {escolhida.PrecoMedio:N2} (base: {escolhida.TotalRegistros} registros, sigla {escolhida.SiglaUnidadeFornecimento})."));
        }

        // Diferença percentual da quantidade
        if (escolhida.QuantidadeMedia > 0)
        {
            var diffQtd = Math.Abs(quantidade - escolhida.QuantidadeMedia) / escolhida.QuantidadeMedia;
            if (diffQtd >= LimiarQuantidadePct)
            {
                var direcao = quantidade > escolhida.QuantidadeMedia ? "acima" : "abaixo";
                divergencias.Add(new DivergenciaValidacao(
                    Local: "Materiais",
                    Linha: linha,
                    Codigo: codigo.ToString(),
                    Tipo: "qtd",
                    ValorPlanilha: quantidade,
                    ValorReferencia: escolhida.QuantidadeMedia,
                    DiferencaPct: diffQtd,
                    SiglaReferencia: escolhida.SiglaUnidadeFornecimento,
                    TotalRegistros: escolhida.TotalRegistros,
                    Mensagem:
                        $"Quantidade {quantidade:N2} está {diffQtd:P0} {direcao} da média histórica " +
                        $"{escolhida.QuantidadeMedia:N2} ({escolhida.SiglaUnidadeFornecimento}, {escolhida.TotalRegistros} registros)."));
            }
        }
    }
}
