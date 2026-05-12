using System.Text.Json;
using Microsoft.Extensions.Logging;
using PcaImporter.Application.Compras.Catalogo;

namespace PcaImporter.Infrastructure.Compras.Catalogo;

public sealed class ComprasGovCatalogoClient : IComprasGovCatalogoClient
{
    private const string CaminhoMaterial = "/modulo-material/4_consultarItemMaterial";
    private const string CaminhoServico = "/modulo-servico/3_consultarItemServico";

    private readonly HttpClient _http;
    private readonly ILogger<ComprasGovCatalogoClient> _log;

    public ComprasGovCatalogoClient(HttpClient http, ILogger<ComprasGovCatalogoClient> log)
    {
        _http = http;
        _log = log;
    }

    public Task<ItemCatalogoDto?> ConsultarMaterialAsync(string codigo, CancellationToken ct = default) =>
        ConsultarAsync(CaminhoMaterial, codigo, "MATERIAL", ct);

    public Task<ItemCatalogoDto?> ConsultarServicoAsync(string codigo, CancellationToken ct = default) =>
        ConsultarAsync(CaminhoServico, codigo, "SERVICO", ct);

    private async Task<ItemCatalogoDto?> ConsultarAsync(string caminho, string codigo, string tipo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var url = $"{caminho}?pagina=1&tamanhoPagina=10&codigoItem={Uri.EscapeDataString(codigo.Trim())}&bps=false";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Accept", "*/*");

        _log.LogInformation("Catalogo {Tipo} consulta {Codigo}", tipo, codigo);
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var corpo = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!resp.IsSuccessStatusCode)
        {
            _log.LogWarning("Catalogo {Tipo} {Codigo} retornou {Status}", tipo, codigo, (int)resp.StatusCode);
            throw new CatalogoHttpException((int)resp.StatusCode, corpo, $"HTTP {(int)resp.StatusCode} no catalogo {tipo}");
        }

        try
        {
            using var doc = JsonDocument.Parse(corpo);
            var raiz = doc.RootElement;
            if (!raiz.TryGetProperty("resultado", out var resultado) || resultado.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var primeiro = resultado.EnumerateArray().FirstOrDefault();
            if (primeiro.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new ItemCatalogoDto(
                CodigoItem: LerInt(primeiro, "codigoItem"),
                CodigoGrupo: LerInt(primeiro, "codigoGrupo"),
                NomeGrupo: LerString(primeiro, "nomeGrupo") ?? string.Empty,
                CodigoClasse: LerInt(primeiro, "codigoClasse"),
                NomeClasse: LerString(primeiro, "nomeClasse") ?? string.Empty,
                CodigoPdm: LerInt(primeiro, "codigoPdm"),
                NomePdm: LerString(primeiro, "nomePdm") ?? string.Empty,
                DescricaoItem: LerString(primeiro, "descricaoItem") ?? string.Empty,
                StatusItem: LerBool(primeiro, "statusItem"),
                ItemSustentavel: LerBool(primeiro, "itemSustentavel"),
                Tipo: tipo,
                DataHoraAtualizacao: LerDataHora(primeiro, "dataHoraAtualizacao"),
                CorpoBruto: corpo
            );
        }
        catch (JsonException ex)
        {
            _log.LogError(ex, "Falha ao parsear resposta do catalogo {Tipo} {Codigo}", tipo, codigo);
            throw new CatalogoHttpException((int)resp.StatusCode, corpo, $"JSON invalido no catalogo {tipo}");
        }
    }

    private static int LerInt(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v) ? v : 0;

    private static bool LerBool(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False) && p.GetBoolean();

    private static string? LerString(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateTimeOffset? LerDataHora(JsonElement el, string nome)
    {
        var s = LerString(el, nome);
        return DateTimeOffset.TryParse(s, out var dt) ? dt : null;
    }
}
