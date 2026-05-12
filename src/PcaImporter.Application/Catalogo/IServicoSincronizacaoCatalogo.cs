namespace PcaImporter.Application.Catalogo;

public enum EstadoSincronizacao
{
    Ocioso = 0,
    Executando = 1,
    Concluida = 2,
    Falhou = 3,
    Cancelada = 4,
}

public sealed class StatusSincronizacaoCatalogo
{
    public EstadoSincronizacao Estado { get; set; } = EstadoSincronizacao.Ocioso;
    public DateTimeOffset? IniciadaEm { get; set; }
    public DateTimeOffset? ConcluidaEm { get; set; }
    public int PaginaAtual { get; set; }
    public int TotalPaginas { get; set; }
    public int ItensProcessados { get; set; }
    public int TotalRegistros { get; set; }
    public int TotalArmazenado { get; set; }
    public DateTimeOffset? UltimaSincronizacao { get; set; }
    public string? UltimoErro { get; set; }
}

public interface IServicoSincronizacaoCatalogo
{
    Task<StatusSincronizacaoCatalogo> ObterStatusAsync(CancellationToken ct = default);

    bool Iniciar();

    bool Cancelar();

    Task<int> LimparAsync(CancellationToken ct = default);
}
