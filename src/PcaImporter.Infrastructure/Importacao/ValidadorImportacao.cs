using PcaImporter.Application.Importacao;

namespace PcaImporter.Infrastructure.Importacao;

public static class ValidadorImportacao
{
    public static (List<ErroValidacao> Erros, List<AvisoValidacao> Avisos) Validar(EntradaImportacaoDfd entrada)
    {
        var erros = new List<ErroValidacao>();
        var avisos = new List<AvisoValidacao>();

        // ----- Aba DFD -----

        // Data
        if (string.IsNullOrWhiteSpace(entrada.DataConclusaoContratacaoTexto))
        {
            erros.Add(new ErroValidacao("DFD", "dataConclusaoContratacao", "Data da conclusão da contratação é obrigatória."));
        }
        else if (entrada.DataConclusaoContratacao is null)
        {
            erros.Add(new ErroValidacao("DFD", "dataConclusaoContratacao",
                $"Data inválida '{entrada.DataConclusaoContratacaoTexto}'. Use o formato AAAA-MM-DD."));
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
            }
        }

        return (erros, avisos);
    }
}
