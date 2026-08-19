using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Token;
using PcaImporter.Domain.Token;

namespace PcaImporter.Infrastructure.Compras;

/// <summary>
/// Detecta quando o processo segue respondendo HTTP, mas o worker responsável
/// pela renovação automática deixou de executar. Ausência de sessão é saudável:
/// nesse estado é necessária ação do usuário, e reiniciar criaria um loop inútil.
/// </summary>
public sealed class KeepAliveTokenHealthCheck : IHealthCheck
{
    private readonly KeepAliveTokenWorker _worker;
    private readonly IGerenciadorTokenSessao _gerenciador;
    private readonly ComprasGovOptions _opcoes;
    private readonly TimeProvider _tempo;

    public KeepAliveTokenHealthCheck(
        KeepAliveTokenWorker worker,
        IGerenciadorTokenSessao gerenciador,
        IOptions<ComprasGovOptions> opcoes,
        TimeProvider tempo)
    {
        _worker = worker;
        _gerenciador = gerenciador;
        _opcoes = opcoes.Value;
        _tempo = tempo;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = _gerenciador.ObterStatus();
        if (status.Estado == EstadoToken.Ausente || !status.TemRefreshToken)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Keep-alive ativo; nenhuma sessão configurada."));
        }

        var heartbeat = _worker.UltimoHeartbeatEm;
        if (heartbeat is null)
        {
            return Task.FromResult(HealthCheckResult.Healthy("Keep-alive iniciando."));
        }

        var toleranciaSegundos = Math.Max(120, _opcoes.Token.IntervaloKeepAliveSegundos * 4);
        var atraso = _tempo.GetUtcNow() - heartbeat.Value;
        if (atraso > TimeSpan.FromSeconds(toleranciaSegundos))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Keep-alive sem heartbeat ha {(long)atraso.TotalSeconds}s."));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            $"Keep-alive operacional; ultimo heartbeat ha {(long)Math.Max(0, atraso.TotalSeconds)}s."));
    }
}
