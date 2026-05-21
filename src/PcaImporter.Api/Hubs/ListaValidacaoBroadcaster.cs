using Microsoft.AspNetCore.SignalR;
using PcaImporter.Application.Validacao;

namespace PcaImporter.Api.Hubs;

/// <summary>
/// Implementação de IEventosListaValidacao que despacha pra todos os clientes
/// conectados ao ListaValidacaoHub. Não é IHostedService — é só uma ponte.
/// </summary>
public sealed class ListaValidacaoBroadcaster : IEventosListaValidacao
{
    private readonly IHubContext<ListaValidacaoHub> _hub;
    private readonly ILogger<ListaValidacaoBroadcaster> _log;

    public ListaValidacaoBroadcaster(IHubContext<ListaValidacaoHub> hub, ILogger<ListaValidacaoBroadcaster> log)
    {
        _hub = hub;
        _log = log;
    }

    public Task LinkAdicionadoAsync(LinkValidacaoDto link) =>
        EmitirAsync(ListaValidacaoHub.EventoLinkAdicionado, link);

    public Task LinkAtualizadoAsync(LinkValidacaoDto link) =>
        EmitirAsync(ListaValidacaoHub.EventoLinkAtualizado, link);

    public Task LinkExcluidoAsync(int linkId, string? porLogin) =>
        EmitirAsync(ListaValidacaoHub.EventoLinkExcluido, new { linkId, porLogin });

    public Task LinkRestauradoAsync(LinkValidacaoDto link) =>
        EmitirAsync(ListaValidacaoHub.EventoLinkRestaurado, link);

    public Task LinkApagadoDefinitivamenteAsync(int linkId) =>
        EmitirAsync(ListaValidacaoHub.EventoLinkApagado, new { linkId });

    public Task ItemRevisadoAsync(int itemId, IReadOnlyList<RevisorDto> revisores) =>
        EmitirAsync(ListaValidacaoHub.EventoItemRevisado, new { itemId, revisores });

    private async Task EmitirAsync(string evento, object payload)
    {
        try
        {
            await _hub.Clients.All.SendAsync(evento, payload);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao publicar {Evento} no ListaValidacaoHub", evento);
        }
    }
}
