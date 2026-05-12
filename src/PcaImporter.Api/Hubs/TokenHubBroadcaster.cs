using Microsoft.AspNetCore.SignalR;
using PcaImporter.Application.Token;

namespace PcaImporter.Api.Hubs;

public sealed class TokenHubBroadcaster : IHostedService
{
    private readonly IGerenciadorTokenSessao _gerenciador;
    private readonly IHubContext<TokenHub> _hub;
    private readonly ILogger<TokenHubBroadcaster> _log;

    public TokenHubBroadcaster(IGerenciadorTokenSessao gerenciador, IHubContext<TokenHub> hub, ILogger<TokenHubBroadcaster> log)
    {
        _gerenciador = gerenciador;
        _hub = hub;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _gerenciador.EstadoMudou += AoMudarEstado;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gerenciador.EstadoMudou -= AoMudarEstado;
        return Task.CompletedTask;
    }

    private void AoMudarEstado(StatusTokenDto status)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _hub.Clients.All.SendAsync(TokenHub.EventoEstadoMudou, status);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Falha ao publicar EstadoMudou no TokenHub");
            }
        });
    }
}
