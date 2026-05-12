using System.Text;
using System.Text.Json;
using PcaImporter.Domain.Token;

namespace PcaImporter.Infrastructure.Compras;

public static class JwtDecoder
{
    public static TokenSessao Decodificar(string bearerOuJwt, string? refreshToken = null)
    {
        var jwt = NormalizarJwt(bearerOuJwt);
        var partes = jwt.Split('.');
        if (partes.Length != 3)
        {
            throw new FormatException("JWT inválido. Esperado formato com 3 partes separadas por ponto.");
        }

        var payloadBytes = DecodificarBase64Url(partes[1]);
        using var doc = JsonDocument.Parse(payloadBytes);
        var raiz = doc.RootElement;

        var sub = raiz.TryGetProperty("sub", out var subEl) ? subEl.GetString() ?? string.Empty : string.Empty;
        var idSessao = raiz.TryGetProperty("id_sessao", out var sesEl) && sesEl.TryGetInt64(out var ses) ? ses : 0L;
        var numeroUasg = raiz.TryGetProperty("numero_uasg", out var uasgEl) && uasgEl.TryGetInt32(out var uasg) ? uasg : 0;
        var iat = raiz.TryGetProperty("iat", out var iatEl) && iatEl.TryGetInt64(out var iatVal) ? iatVal : 0L;
        var exp = raiz.TryGetProperty("exp", out var expEl) && expEl.TryGetInt64(out var expVal) ? expVal : 0L;
        var tipoAuth = raiz.TryGetProperty("autenticacao", out var authEl) ? authEl.GetString() ?? string.Empty : string.Empty;

        var mnemonicos = new List<string>();
        if (raiz.TryGetProperty("mnemonicos", out var mnEl) && mnEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in mnEl.EnumerateArray())
            {
                var s = item.GetString();
                if (!string.IsNullOrEmpty(s)) mnemonicos.Add(s);
            }
        }

        if (exp == 0)
        {
            throw new FormatException("JWT sem claim 'exp'. Token inválido para gerenciamento.");
        }

        return new TokenSessao(
            AccessToken: jwt,
            RefreshToken: string.IsNullOrWhiteSpace(refreshToken) ? null : NormalizarJwt(refreshToken),
            EmitidoEm: DateTimeOffset.FromUnixTimeSeconds(iat),
            ExpiraEm: DateTimeOffset.FromUnixTimeSeconds(exp),
            Sub: sub,
            IdSessao: idSessao,
            NumeroUasg: numeroUasg,
            Mnemonicos: mnemonicos,
            TipoAutenticacao: tipoAuth
        );
    }

    public static string NormalizarJwt(string bearerOuJwt)
    {
        var s = bearerOuJwt.Trim();
        if (s.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            s = s[7..].Trim();
        }
        return s;
    }

    private static byte[] DecodificarBase64Url(string base64Url)
    {
        var s = base64Url.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
