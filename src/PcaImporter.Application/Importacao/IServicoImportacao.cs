namespace PcaImporter.Application.Importacao;

public interface IServicoImportacao
{
    Task<ResultadoValidacaoImportacao> ValidarAsync(Stream xlsx, CancellationToken ct = default);

    Task<ResultadoValidacaoImportacao> ValidarLinkAsync(string url, CancellationToken ct = default);

    Task<string> IniciarAsync(Stream xlsx, CancellationToken ct = default);

    Task<string> IniciarLinkAsync(string url, bool confirmarDuplicado, string? usuarioLogin, CancellationToken ct = default);

    Task<HistoricoImportacaoDto?> ConsultarHistoricoPorLinkAsync(string url, CancellationToken ct = default);

    Task<IReadOnlyList<HistoricoImportacaoDto>> ListarHistoricoAsync(int limite = 200, CancellationToken ct = default);

    Task<MetricasImportacaoDto> ObterMetricasAsync(CancellationToken ct = default);

    Task<int> ResetarMetricasAsync(CancellationToken ct = default);

    StatusImportacao? ObterStatus(string id);

    IReadOnlyList<StatusImportacao> ListarRecentes(int limite = 20);
}

public sealed class ImportacaoDuplicadaException : Exception
{
    public HistoricoImportacaoDto Anterior { get; }
    public ImportacaoDuplicadaException(HistoricoImportacaoDto anterior, string mensagem) : base(mensagem)
    {
        Anterior = anterior;
    }
}
