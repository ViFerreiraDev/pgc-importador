using Microsoft.AspNetCore.SignalR;
using PcaImporter.Application.Token;

namespace PcaImporter.Api.Hubs;

public sealed class TokenHub : Hub
{
    public const string Caminho = "/hubs/token";
    public const string EventoEstadoMudou = "EstadoMudou";

    private readonly IGerenciadorTokenSessao _gerenciador;

    public TokenHub(IGerenciadorTokenSessao gerenciador)
    {
        _gerenciador = gerenciador;
    }

    public override async Task OnConnectedAsync()
    {
        await Clients.Caller.SendAsync(EventoEstadoMudou, _gerenciador.ObterStatus());
        await base.OnConnectedAsync();
    }
}
