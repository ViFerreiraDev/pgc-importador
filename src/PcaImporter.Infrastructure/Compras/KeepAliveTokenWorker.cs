using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Token;

namespace PcaImporter.Infrastructure.Compras;

public sealed class KeepAliveTokenWorker : BackgroundService
{
    private readonly GerenciadorTokenSessao _gerenciador;
    private readonly ComprasGovOptions _opcoes;
    private readonly ILogger<KeepAliveTokenWorker> _log;

    public KeepAliveTokenWorker(
        IGerenciadorTokenSessao gerenciador,
        IOptions<ComprasGovOptions> opcoes,
        ILogger<KeepAliveTokenWorker> log)
    {
        _gerenciador = (GerenciadorTokenSessao)gerenciador;
        _opcoes = opcoes.Value;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("KeepAliveTokenWorker iniciado. Tick {Seg}s, limiar refresh {Limiar}s.",
            _opcoes.Token.IntervaloKeepAliveSegundos, _opcoes.Token.LimiarRefreshSegundos);

        var intervalo = TimeSpan.FromSeconds(Math.Max(5, _opcoes.Token.IntervaloKeepAliveSegundos));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _gerenciador.RefreshSeNecessarioAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Tick do KeepAlive falhou");
            }

            try
            {
                await Task.Delay(intervalo, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _log.LogInformation("KeepAliveTokenWorker encerrado.");
    }
}
