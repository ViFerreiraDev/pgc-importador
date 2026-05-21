using Microsoft.AspNetCore.SignalR;

namespace PcaImporter.Api.Hubs;

public sealed class ListaValidacaoHub : Hub
{
    public const string Caminho = "/hubs/lista-validacao";

    public const string EventoLinkAdicionado            = "LinkAdicionado";
    public const string EventoLinkAtualizado            = "LinkAtualizado";
    public const string EventoLinkExcluido              = "LinkExcluido";
    public const string EventoLinkRestaurado            = "LinkRestaurado";
    public const string EventoLinkApagado               = "LinkApagado";
    public const string EventoItemRevisado              = "ItemRevisado";
}
