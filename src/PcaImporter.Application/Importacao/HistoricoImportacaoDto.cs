namespace PcaImporter.Application.Importacao;

public sealed record HistoricoImportacaoDto(
    int Id,
    string IdPlanilha,
    string UrlOriginal,
    DateTimeOffset ImportadaEm,
    string IdExecucao,
    int NumeroDfd,
    int AnoDfd,
    long IdArtefato,
    long IdFormalizacaoDemanda,
    int TotalMateriais,
    decimal ValorTotal,
    bool Sucesso,
    string? MensagemErro,
    int? LinhaErro,
    string? Descricao,
    string? UsuarioLogin
);

public sealed record MetricasImportacaoDto(
    int TotalPlanilhas,
    int TotalItens,
    decimal TotalValor
);

public interface IRepositorioHistoricoImportacao
{
    Task<HistoricoImportacaoDto?> BuscarPorIdPlanilhaAsync(string idPlanilha, CancellationToken ct = default);

    Task RegistrarAsync(HistoricoImportacaoDto registro, CancellationToken ct = default);

    Task<IReadOnlyList<HistoricoImportacaoDto>> ListarAsync(int limite = 200, CancellationToken ct = default);

    Task<MetricasImportacaoDto> ObterMetricasAsync(CancellationToken ct = default);

    Task<int> LimparAsync(CancellationToken ct = default);
}
