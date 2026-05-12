using System.Globalization;
using System.Text;
using PcaImporter.Application.Compras.Dfd;

namespace PcaImporter.Infrastructure.Importacao;

public static class Validadores
{
    /// <summary>
    /// Valida CPF brasileiro: 11 dígitos numéricos com os dois dígitos verificadores corretos.
    /// Rejeita sequências repetidas (00000000000, 11111111111 etc).
    /// </summary>
    public static bool EhCpfValido(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf)) return false;
        var digitos = new string(cpf.Where(char.IsDigit).ToArray());
        if (digitos.Length != 11) return false;
        if (digitos.Distinct().Count() == 1) return false;

        var d = digitos.Select(c => c - '0').ToArray();

        var soma1 = 0;
        for (var i = 0; i < 9; i++) soma1 += d[i] * (10 - i);
        var dv1 = soma1 * 10 % 11;
        if (dv1 == 10) dv1 = 0;
        if (dv1 != d[9]) return false;

        var soma2 = 0;
        for (var i = 0; i < 10; i++) soma2 += d[i] * (11 - i);
        var dv2 = soma2 * 10 % 11;
        if (dv2 == 10) dv2 = 0;
        return dv2 == d[10];
    }

    /// <summary>
    /// Normaliza valor de prioridade tolerando acentos e gênero.
    /// Aceita: BAIXO/BAIXA, MEDIO/MEDIA/MÉDIO/MÉDIA, ALTO/ALTA (em qualquer caixa).
    /// Pipeline: trim → upper → remove acentos → mapeia para BAIXO/MEDIO/ALTO.
    /// </summary>
    public static (NivelPrioridade? Prioridade, string? Erro) NormalizarPrioridade(string? bruto)
    {
        if (string.IsNullOrWhiteSpace(bruto))
        {
            return (null, "Prioridade obrigatória.");
        }

        var s = RemoverAcentos(bruto.Trim().ToUpperInvariant());

        return s switch
        {
            "BAIXO" or "BAIXA" => (NivelPrioridade.BAIXO, null),
            "MEDIO" or "MEDIA" => (NivelPrioridade.MEDIO, null),
            "ALTO" or "ALTA" => (NivelPrioridade.ALTO, null),
            _ => (null, $"Prioridade inválida '{bruto}'. Use BAIXO, MEDIO ou ALTO."),
        };
    }

    /// <summary>
    /// Código CATMAT/CATSER: exatamente 6 dígitos numéricos, sem outros caracteres.
    /// </summary>
    public static bool EhCodigoBrValido(string? codigo)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        var s = codigo.Trim();
        return s.Length == 6 && s.All(char.IsDigit);
    }

    private static string RemoverAcentos(string s)
    {
        var n = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(n.Length);
        foreach (var c in n)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
