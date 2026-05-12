using System.Text.RegularExpressions;

namespace PcaImporter.Infrastructure.Importacao;

public static class GoogleSheetsHelper
{
    private static readonly Regex RxIdNaUrl = new(
        @"docs\.google\.com/spreadsheets/d/(?<id>[a-zA-Z0-9_-]{20,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool TentarExtrairId(string url, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrWhiteSpace(url)) return false;
        var match = RxIdNaUrl.Match(url);
        if (!match.Success) return false;
        id = match.Groups["id"].Value;
        return true;
    }

    public static string ConstruirUrlExportXlsx(string id) =>
        $"https://docs.google.com/spreadsheets/d/{id}/export?format=xlsx";

    public static async Task<Stream> BaixarComoXlsxAsync(HttpClient http, string url, CancellationToken ct)
    {
        if (!TentarExtrairId(url, out var id))
        {
            throw new InvalidOperationException(
                "URL do Google Sheets inválida. Esperado algo como https://docs.google.com/spreadsheets/d/<ID>/...");
        }

        var exportUrl = ConstruirUrlExportXlsx(id);
        using var resp = await http.GetAsync(exportUrl, ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            // 404 normalmente significa: planilha não existe OU está restrita (Google esconde).
            // 401/403 são acesso negado direto.
            var status = (int)resp.StatusCode;
            if (status == 401 || status == 403 || status == 404)
            {
                throw new InvalidOperationException(MensagemPlanilhaRestrita());
            }
            throw new InvalidOperationException(
                $"Não foi possível baixar a planilha (HTTP {status}). Tente novamente em instantes.");
        }

        var contentType = resp.Content.Headers.ContentType?.MediaType ?? string.Empty;
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

        // Google responde 200 com HTML de login quando a planilha não é pública.
        if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase) || EhHtmlInicio(bytes))
        {
            throw new InvalidOperationException(MensagemPlanilhaRestrita());
        }

        return new MemoryStream(bytes);
    }

    public static string MensagemPlanilhaRestrita() =>
        "A planilha está restrita. Para deixá-la pública: " +
        "1) Abra a planilha no Google Sheets. " +
        "2) Clique em 'Compartilhar' (canto superior direito). " +
        "3) Em 'Acesso geral', troque para 'Qualquer pessoa com o link'. " +
        "4) Mantenha o papel como 'Leitor'. " +
        "5) Copie o link e cole aqui novamente.";

    private static bool EhHtmlInicio(byte[] bytes)
    {
        if (bytes.Length < 5) return false;
        var inicio = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(64, bytes.Length)).TrimStart();
        return inicio.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || inicio.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }
}
