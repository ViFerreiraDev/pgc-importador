using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Token;

namespace PcaImporter.Infrastructure.Compras;

public sealed class ComprasGovTokenClient : IComprasGovTokenClient
{
    private readonly HttpClient _http;
    private readonly ComprasGovOptions _opcoes;
    private readonly ILogger<ComprasGovTokenClient> _log;

    public ComprasGovTokenClient(HttpClient http, IOptions<ComprasGovOptions> opcoes, ILogger<ComprasGovTokenClient> log)
    {
        _http = http;
        _opcoes = opcoes.Value;
        _log = log;
    }

    public async Task<RespostaRefreshToken> RefreshAsync(string refreshTokenAtual, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshTokenAtual))
        {
            return new RespostaRefreshToken(false, 0, null, null, string.Empty, "Refresh token ausente");
        }

        var refresh = JwtDecoder.NormalizarJwt(refreshTokenAtual);

        using var req = new HttpRequestMessage(HttpMethod.Get, _opcoes.Token.CaminhoRetoken);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refresh);
        req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");

        try
        {
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var corpo = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                var headers = string.Join(", ", resp.Headers.Select(h => $"{h.Key}={string.Join(";", h.Value)}"));
                _log.LogWarning("Refresh do token retornou {Status}. Headers: {Headers}. Corpo: {Corpo}",
                    (int)resp.StatusCode, Truncar(headers, 800), Truncar(corpo));
                return new RespostaRefreshToken(false, (int)resp.StatusCode, null, null, corpo, $"HTTP {(int)resp.StatusCode}");
            }

            var (novoAccess, novoRefresh) = ExtrairTokens(corpo, resp);

            if (string.IsNullOrWhiteSpace(novoAccess))
            {
                _log.LogError("Refresh respondeu {Status} mas nao foi possivel extrair access token. Corpo: {Corpo}",
                    (int)resp.StatusCode, Truncar(corpo));
                return new RespostaRefreshToken(false, (int)resp.StatusCode, null, null, corpo, "Resposta sem access token reconhecivel");
            }

            return new RespostaRefreshToken(true, (int)resp.StatusCode, novoAccess, novoRefresh, corpo, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Falha ao chamar retoken");
            return new RespostaRefreshToken(false, 0, null, null, string.Empty, ex.Message);
        }
    }

    private static (string? Access, string? Refresh) ExtrairTokens(string corpo, HttpResponseMessage resp)
    {
        string? access = null;
        string? refresh = null;

        if (resp.Headers.TryGetValues("Authorization", out var values))
        {
            foreach (var v in values)
            {
                var jwt = JwtDecoder.NormalizarJwt(v);
                if (PareceJwt(jwt)) { access = jwt; break; }
            }
        }

        if (!string.IsNullOrWhiteSpace(corpo))
        {
            try
            {
                using var doc = JsonDocument.Parse(corpo);
                var raiz = doc.RootElement;
                if (raiz.ValueKind == JsonValueKind.Object)
                {
                    if (access is null)
                    {
                        access = ExtrairCampo(raiz, CamposAccessToken);
                    }
                    refresh = ExtrairCampo(raiz, CamposRefreshToken);
                }
            }
            catch (JsonException)
            {
            }
        }

        if (access is null && PareceJwt(corpo.Trim()))
        {
            access = corpo.Trim();
        }

        return (access, refresh);
    }

    private static readonly string[] CamposAccessToken =
    [
        "accessToken", "access_token",
        "token", "bearer", "jwt",
        "id_token", "idToken",
        "novoToken", "novo_token"
    ];

    private static readonly string[] CamposRefreshToken =
    [
        "refreshToken", "refresh_token",
        "novoRefreshToken", "novo_refresh_token"
    ];

    private static string? ExtrairCampo(JsonElement raiz, string[] candidatos)
    {
        foreach (var nome in candidatos)
        {
            if (raiz.TryGetProperty(nome, out var el) && el.ValueKind == JsonValueKind.String)
            {
                var valor = JwtDecoder.NormalizarJwt(el.GetString() ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(valor)) return valor;
            }
        }
        return null;
    }

    private static bool PareceJwt(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var partes = s.Split('.');
        return partes.Length == 3 && partes[0].Length > 4 && partes[1].Length > 4;
    }

    private static string Truncar(string s, int max = 500) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "...[truncado]");
}
