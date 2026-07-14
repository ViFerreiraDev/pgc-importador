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

    // O refresh token do ComprasGov vive ~30 min; retentar por ~1-2 min cobre
    // instabilidade na subida sem estourar a janela de validade.
    private const int MaxTentativas = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string? refresh;
        try
        {
            refresh = await _repo.LerRefreshAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao ler refresh persistido no bootstrap");
            return;
        }

        if (string.IsNullOrWhiteSpace(refresh))
        {
            _log.LogInformation("Sem refresh persistido. Aguardando paste manual via UI.");
            return;
        }

        _log.LogInformation("Refresh persistido encontrado, fazendo bootstrap automatico.");

        for (var tentativa = 1; tentativa <= MaxTentativas && !stoppingToken.IsCancellationRequested; tentativa++)
        {
            try
            {
                await _gerenciador.DefinirAPartirDoRefreshAsync(refresh, stoppingToken).ConfigureAwait(false);
                _log.LogInformation("Bootstrap automatico OK (tentativa {T}).", tentativa);
                return;
            }
            catch (RefreshInicialFalhouException ex) when (ex.StatusHttp is 401 or 403)
            {
                // Rejeição definitiva: o refresh realmente expirou/foi invalidado no servidor.
                _log.LogWarning(ex, "Refresh persistido rejeitado pelo servidor (HTTP {Status}). Limpando.", ex.StatusHttp);
                try { await _repo.LimparAsync(stoppingToken).ConfigureAwait(false); } catch { }
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Transitório (rede fora, ComprasGov instável, timeout): mantém o refresh
                // persistido — ele pode ainda ser válido — e tenta de novo com backoff.
                if (tentativa == MaxTentativas)
                {
                    _log.LogError(ex,
                        "Bootstrap automatico falhou apos {N} tentativas. Refresh persistido mantido; " +
                        "sessão pode ser retomada via paste manual ou novo restart.", MaxTentativas);
                    return;
                }

                var esperaSeg = Math.Min(60, 5 * (1 << (tentativa - 1))); // 5, 10, 20, 40
                _log.LogWarning(ex, "Bootstrap automatico falhou (tentativa {T}/{N}). Nova tentativa em {S}s.",
                    tentativa, MaxTentativas, esperaSeg);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(esperaSeg), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}
