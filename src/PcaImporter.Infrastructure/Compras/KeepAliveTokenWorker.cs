using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Token;

namespace PcaImporter.Infrastructure.Compras;

public sealed class KeepAliveTokenWorker : BackgroundService
{
    private readonly IGerenciadorTokenSessao _gerenciador;
    private readonly ComprasGovOptions _opcoes;
    private readonly ILogger<KeepAliveTokenWorker> _log;
    private readonly TimeProvider _tempo;
    private long _ultimoHeartbeatUnixMs;

    public KeepAliveTokenWorker(
        IGerenciadorTokenSessao gerenciador,
        IOptions<ComprasGovOptions> opcoes,
        ILogger<KeepAliveTokenWorker> log,
        TimeProvider tempo)
    {
        _gerenciador = gerenciador;
        _opcoes = opcoes.Value;
        _log = log;
        _tempo = tempo;
    }

    public DateTimeOffset? UltimoHeartbeatEm
    {
        get
        {
            var unixMs = Interlocked.Read(ref _ultimoHeartbeatUnixMs);
            return unixMs == 0 ? null : DateTimeOffset.FromUnixTimeMilliseconds(unixMs);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("KeepAliveTokenWorker iniciado. Tick {Seg}s, limiar refresh {Limiar}s.",
            _opcoes.Token.IntervaloKeepAliveSegundos, _opcoes.Token.LimiarRefreshSegundos);

        var intervalo = TimeSpan.FromSeconds(Math.Max(5, _opcoes.Token.IntervaloKeepAliveSegundos));

        // Executa imediatamente e depois em cadência estável. O PeriodicTimer evita
        // acumular atraso do tempo gasto em cada ciclo.
        await ExecutarCicloAsync(stoppingToken).ConfigureAwait(false);
        using var timer = new PeriodicTimer(intervalo, _tempo);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await ExecutarCicloAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do host.
        }

        _log.LogInformation("KeepAliveTokenWorker encerrado.");
    }

    internal async Task ExecutarCicloAsync(CancellationToken stoppingToken)
    {
        RegistrarHeartbeat();
        try
        {
            await _gerenciador.RefreshSeNecessarioAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            // Timeout de rede não pode encerrar permanentemente o keep-alive.
            _log.LogWarning(ex, "Tick do KeepAlive cancelado por timeout; worker continuara ativo");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Tick do KeepAlive falhou; worker continuara ativo");
        }
        finally
        {
            RegistrarHeartbeat();
        }
    }

    private void RegistrarHeartbeat() =>
        Interlocked.Exchange(ref _ultimoHeartbeatUnixMs, _tempo.GetUtcNow().ToUnixTimeMilliseconds());
}
