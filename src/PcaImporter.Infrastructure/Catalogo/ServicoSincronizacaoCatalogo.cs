using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PcaImporter.Application.Catalogo;
using PcaImporter.Application.Logs;

namespace PcaImporter.Infrastructure.Catalogo;

public sealed class ServicoSincronizacaoCatalogo : IServicoSincronizacaoCatalogo
{
    private const int TamanhoPagina = 500;
    private const int IntervaloEntrePaginasMs = 800;
    private const int MaxTentativas = 6;
    private const string Endpoint = "modulo-material/4_consultarItemMaterial";

    private readonly IHttpClientFactory _httpFactory;
    private readonly IRepositorioCatalogoMaterial _repo;
    private readonly IRegistroLogs _logs;
    private readonly ILogger<ServicoSincronizacaoCatalogo> _log;

    private readonly Lock _trava = new();
    private readonly StatusSincronizacaoCatalogo _status = new();
    private CancellationTokenSource? _cts;
    private Task? _execucao;

    public ServicoSincronizacaoCatalogo(
        IHttpClientFactory httpFactory,
        IRepositorioCatalogoMaterial repo,
        IRegistroLogs logs,
        ILogger<ServicoSincronizacaoCatalogo> log)
    {
        _httpFactory = httpFactory;
        _repo = repo;
        _logs = logs;
        _log = log;
    }

    public async Task<StatusSincronizacaoCatalogo> ObterStatusAsync(CancellationToken ct = default)
    {
        var snap = Snapshot();
        snap.TotalArmazenado = await _repo.ContarAsync(ct).ConfigureAwait(false);
        snap.UltimaSincronizacao = await _repo.UltimaSincronizacaoAsync(ct).ConfigureAwait(false);
        return snap;
    }

    public bool Iniciar()
    {
        lock (_trava)
        {
            if (_status.Estado == EstadoSincronizacao.Executando) return false;
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _status.Estado = EstadoSincronizacao.Executando;
            _status.IniciadaEm = DateTimeOffset.UtcNow;
            _status.ConcluidaEm = null;
            _status.PaginaAtual = 0;
            _status.TotalPaginas = 0;
            _status.ItensProcessados = 0;
            _status.TotalRegistros = 0;
            _status.UltimoErro = null;
            _execucao = Task.Run(() => ExecutarAsync(_cts.Token));
            return true;
        }
    }

    public bool Cancelar()
    {
        lock (_trava)
        {
            if (_status.Estado != EstadoSincronizacao.Executando) return false;
            _cts?.Cancel();
            return true;
        }
    }

    public Task<int> LimparAsync(CancellationToken ct = default) => _repo.LimparAsync(ct);

    private StatusSincronizacaoCatalogo Snapshot()
    {
        lock (_trava)
        {
            return new StatusSincronizacaoCatalogo
            {
                Estado = _status.Estado,
                IniciadaEm = _status.IniciadaEm,
                ConcluidaEm = _status.ConcluidaEm,
                PaginaAtual = _status.PaginaAtual,
                TotalPaginas = _status.TotalPaginas,
                ItensProcessados = _status.ItensProcessados,
                TotalRegistros = _status.TotalRegistros,
                UltimoErro = _status.UltimoErro,
            };
        }
    }

    private async Task ExecutarAsync(CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("dadosabertos");
        var pagina = 1;
        try
        {
            _logs.Registrar(NivelLog.Info, "Catalogo", "Sincronização do catálogo iniciada.");
            while (!ct.IsCancellationRequested)
            {
                var url = $"{Endpoint}?pagina={pagina}&tamanhoPagina={TamanhoPagina}&bps=false";
                var resp = await TentarBaixarAsync(http, url, ct).ConfigureAwait(false);
                if (resp is null)
                {
                    throw new InvalidOperationException($"Falha ao baixar página {pagina} do catálogo.");
                }

                var agora = DateTimeOffset.UtcNow;
                var lote = resp.Resultado.Select(r => new ItemCatalogoLocalDto(
                    CodigoItem: r.CodigoItem,
                    CodigoGrupo: r.CodigoGrupo,
                    NomeGrupo: r.NomeGrupo ?? string.Empty,
                    CodigoClasse: r.CodigoClasse,
                    NomeClasse: r.NomeClasse ?? string.Empty,
                    CodigoPdm: r.CodigoPdm,
                    NomePdm: r.NomePdm ?? string.Empty,
                    DescricaoItem: r.DescricaoItem ?? string.Empty,
                    StatusItem: r.StatusItem,
                    ItemSustentavel: r.ItemSustentavel,
                    CodigoNcm: r.CodigoNcm,
                    AtualizadoEmFonte: ParsearData(r.DataHoraAtualizacao),
                    SincronizadoEm: agora
                )).ToList();

                await _repo.UpsertEmLoteAsync(lote, ct).ConfigureAwait(false);

                lock (_trava)
                {
                    _status.PaginaAtual = pagina;
                    _status.TotalPaginas = resp.TotalPaginas;
                    _status.TotalRegistros = resp.TotalRegistros;
                    _status.ItensProcessados += lote.Count;
                    _status.UltimoErro = null;
                }

                if (resp.PaginasRestantes <= 0) break;
                pagina++;
                await Task.Delay(IntervaloEntrePaginasMs, ct).ConfigureAwait(false);
            }

            lock (_trava)
            {
                _status.Estado = ct.IsCancellationRequested ? EstadoSincronizacao.Cancelada : EstadoSincronizacao.Concluida;
                _status.ConcluidaEm = DateTimeOffset.UtcNow;
            }
            _logs.Registrar(NivelLog.Sucesso, "Catalogo",
                $"Sincronização do catálogo finalizada ({_status.ItensProcessados} itens em {pagina} páginas).");
        }
        catch (OperationCanceledException)
        {
            lock (_trava)
            {
                _status.Estado = EstadoSincronizacao.Cancelada;
                _status.ConcluidaEm = DateTimeOffset.UtcNow;
            }
            _logs.Registrar(NivelLog.Aviso, "Catalogo", "Sincronização do catálogo cancelada.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao sincronizar catálogo na página {Pagina}", pagina);
            lock (_trava)
            {
                _status.Estado = EstadoSincronizacao.Falhou;
                _status.ConcluidaEm = DateTimeOffset.UtcNow;
                _status.UltimoErro = ex.Message;
            }
            _logs.Registrar(NivelLog.Erro, "Catalogo", $"Sincronização falhou na página {pagina}", ex.Message);
        }
    }

    private async Task<RespostaDadosAbertos?> TentarBaixarAsync(HttpClient http, string url, CancellationToken ct)
    {
        for (var tent = 1; tent <= MaxTentativas; tent++)
        {
            try
            {
                var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var status = (int)resp.StatusCode;
                    if (status is 429 or >= 500)
                    {
                        var espera = CalcularEspera(resp, tent);
                        _log.LogWarning("HTTP {Status} ao baixar catálogo (tentativa {Tent}/{Max}). Aguardando {Seg}s antes de retry.",
                            status, tent, MaxTentativas, (int)espera.TotalSeconds);
                        AnotarEsperaRateLimit(espera);
                        await Task.Delay(espera, ct).ConfigureAwait(false);
                        continue;
                    }
                    var corpo = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    throw new InvalidOperationException($"HTTP {status}: {corpo}");
                }
                return await resp.Content.ReadFromJsonAsync<RespostaDadosAbertos>(cancellationToken: ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) when (tent < MaxTentativas)
            {
                var espera = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, tent)));
                _log.LogWarning(ex, "Falha ao baixar catálogo (tentativa {Tent}/{Max}). Aguardando {Seg}s.",
                    tent, MaxTentativas, (int)espera.TotalSeconds);
                await Task.Delay(espera, ct).ConfigureAwait(false);
            }
        }
        return null;
    }

    private static TimeSpan CalcularEspera(HttpResponseMessage resp, int tentativa)
    {
        // honra Retry-After se vier
        if (resp.Headers.RetryAfter is { } ra)
        {
            if (ra.Delta.HasValue) return ra.Delta.Value;
            if (ra.Date.HasValue)
            {
                var dif = ra.Date.Value - DateTimeOffset.UtcNow;
                if (dif > TimeSpan.Zero) return dif;
            }
        }
        // backoff exponencial 2,4,8,16,32,60 (cap)
        return TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, tentativa)));
    }

    private void AnotarEsperaRateLimit(TimeSpan espera)
    {
        lock (_trava)
        {
            _status.UltimoErro = $"Rate limit (HTTP 429). Aguardando {(int)espera.TotalSeconds}s antes de tentar novamente.";
        }
    }

    private static DateTimeOffset? ParsearData(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        if (DateTimeOffset.TryParse(texto, out var dto)) return dto;
        if (DateTime.TryParse(texto, out var dt)) return new DateTimeOffset(dt, TimeSpan.Zero);
        return null;
    }

    private sealed record RespostaDadosAbertos(
        [property: JsonPropertyName("resultado")] List<ItemDadosAbertos> Resultado,
        [property: JsonPropertyName("totalRegistros")] int TotalRegistros,
        [property: JsonPropertyName("totalPaginas")] int TotalPaginas,
        [property: JsonPropertyName("paginasRestantes")] int PaginasRestantes
    );

    private sealed record ItemDadosAbertos(
        [property: JsonPropertyName("codigoItem")] int CodigoItem,
        [property: JsonPropertyName("codigoGrupo")] int CodigoGrupo,
        [property: JsonPropertyName("nomeGrupo")] string? NomeGrupo,
        [property: JsonPropertyName("codigoClasse")] int CodigoClasse,
        [property: JsonPropertyName("nomeClasse")] string? NomeClasse,
        [property: JsonPropertyName("codigoPdm")] int CodigoPdm,
        [property: JsonPropertyName("nomePdm")] string? NomePdm,
        [property: JsonPropertyName("descricaoItem")] string? DescricaoItem,
        [property: JsonPropertyName("statusItem")] bool StatusItem,
        [property: JsonPropertyName("itemSustentavel")] bool ItemSustentavel,
        [property: JsonPropertyName("codigo_ncm")] string? CodigoNcm,
        [property: JsonPropertyName("dataHoraAtualizacao")] string? DataHoraAtualizacao
    );
}
