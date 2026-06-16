namespace PcaImporter.Application.Validacao;

public interface IServicoListaValidacao
{
    Task<ListaValidacaoDto> ObterListaAsync(CancellationToken ct = default);

    Task<LinkValidacaoDto?> AdicionarLinkAsync(string url, string? rotulo, string? classe, int? numeroGrupo, string? loginAtor, CancellationToken ct = default);

    Task<IReadOnlyList<LinkValidacaoDto>> AdicionarLinksDoTextoAsync(string textoColado, string? classe, bool detectarNumeroGrupo, string? loginAtor, CancellationToken ct = default);

    /// <summary>
    /// Compara o texto colado com a lista ativa **da classe alvo**: adiciona novos,
    /// indica duplicados, e indica ausentes (links da classe que não vieram no texto).
    /// </summary>
    Task<DiffLoteDto> CompararLoteAsync(string textoColado, string? classe, bool detectarNumeroGrupo, string? loginAtor, CancellationToken ct = default);

    Task<LinkValidacaoDto?> ValidarLinkAsync(int linkId, CancellationToken ct = default);

    Task ExcluirLinkAsync(int linkId, string? loginAtor, CancellationToken ct = default);

    Task RestaurarLinkAsync(int linkId, string? loginAtor, CancellationToken ct = default);

    Task ApagarDefinitivamenteAsync(int linkId, string? loginAtor, CancellationToken ct = default);

    Task<IReadOnlyList<RevisorDto>> RevisarItemAsync(int itemId, string login, CancellationToken ct = default);

    Task<IReadOnlyList<RevisorDto>> DesrevisarItemAsync(int itemId, string login, CancellationToken ct = default);

    /// <summary>
    /// Registra o desfecho de uma tentativa de importação disparada a partir deste link.
    /// Em sucesso, marca ImportadoEm e limpa UltimoErroImportacao. Em falha, registra a mensagem.
    /// </summary>
    Task<LinkValidacaoDto?> RegistrarResultadoImportacaoAsync(int linkId, string idExecucao, bool sucesso, string? mensagemErro, CancellationToken ct = default);
}

public sealed class LinkJaCadastradoException : Exception
{
    public string IdPlanilha { get; }
    public LinkJaCadastradoException(string idPlanilha)
        : base($"A planilha {idPlanilha} já está na lista ativa.")
    {
        IdPlanilha = idPlanilha;
    }
}

public sealed class LinkNaoEncontradoException : Exception
{
    public LinkNaoEncontradoException(int id) : base($"Link {id} não encontrado.") { }
}
