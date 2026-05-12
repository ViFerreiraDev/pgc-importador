namespace PcaImporter.Application.Token;

public interface IRepositorioTokenSessao
{
    Task<string?> LerRefreshAsync(CancellationToken ct = default);

    Task SalvarRefreshAsync(string refreshToken, CancellationToken ct = default);

    Task LimparAsync(CancellationToken ct = default);
}
