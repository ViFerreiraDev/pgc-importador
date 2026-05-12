namespace PcaImporter.Application.Token;

public interface IComprasGovTokenClient
{
    Task<RespostaRefreshToken> RefreshAsync(string refreshTokenAtual, CancellationToken ct = default);
}

public sealed record RespostaRefreshToken(
    bool Sucesso,
    int StatusHttp,
    string? NovoAccessToken,
    string? NovoRefreshToken,
    string CorpoBruto,
    string? Erro
);
