namespace PcaImporter.Application.Validacao;

/// <summary>
/// Contrato pra publicar eventos da lista de validação.
/// Implementação real fica no broadcaster (SignalR). Em testes, mock simples.
/// </summary>
public interface IEventosListaValidacao
{
    Task LinkAdicionadoAsync(LinkValidacaoDto link);
    Task LinkAtualizadoAsync(LinkValidacaoDto link);
    Task LinkExcluidoAsync(int linkId, string? porLogin);
    Task LinkRestauradoAsync(LinkValidacaoDto link);
    Task LinkApagadoDefinitivamenteAsync(int linkId);
    Task ItemRevisadoAsync(int itemId, IReadOnlyList<RevisorDto> revisores);
}
