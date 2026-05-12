using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PcaImporter.Application.Token;

namespace PcaImporter.Infrastructure.Compras;

public sealed class BootstrapTokenAoIniciar : BackgroundService
{
    private readonly IRepositorioTokenSessao _repo;
    private readonly IGerenciadorTokenSessao _gerenciador;
    private readonly ILogger<BootstrapTokenAoIniciar> _log;

    public BootstrapTokenAoIniciar(
        IRepositorioTokenSessao repo,
        IGerenciadorTokenSessao gerenciador,
        ILogger<BootstrapTokenAoIniciar> log)
    {
        _repo = repo;
        _gerenciador = gerenciador;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var refresh = await _repo.LerRefreshAsync(stoppingToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(refresh))
            {
                _log.LogInformation("Sem refresh persistido. Aguardando paste manual via UI.");
                return;
            }

            _log.LogInformation("Refresh persistido encontrado, fazendo bootstrap automatico.");
            await _gerenciador.DefinirAPartirDoRefreshAsync(refresh, stoppingToken).ConfigureAwait(false);
            _log.LogInformation("Bootstrap automatico OK.");
        }
        catch (RefreshInicialFalhouException ex)
        {
            _log.LogWarning(ex, "Refresh persistido invalido (provavelmente expirou). Limpando.");
            try { await _repo.LimparAsync(stoppingToken).ConfigureAwait(false); } catch { }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha no bootstrap automatico do token");
        }
    }
}
