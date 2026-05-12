using PcaImporter.Domain.Token;

namespace PcaImporter.Application.Token;

public interface IGerenciadorTokenSessao
{
    StatusTokenDto ObterStatus();

    Task<StatusTokenDto> DefinirAPartirDoRefreshAsync(string refreshToken, CancellationToken ct = default);

    Task<TokenSessao> ObterTokenValidoAsync(CancellationToken ct = default);

    Task<StatusTokenDto> ForcarRefreshAsync(CancellationToken ct = default);

    void Limpar();

    event Action<StatusTokenDto>? EstadoMudou;
}

public sealed class TokenIndisponivelException : Exception
{
    public TokenIndisponivelException(string mensagem) : base(mensagem) { }
}

public sealed class RefreshInicialFalhouException : Exception
{
    public int StatusHttp { get; }
    public string CorpoBruto { get; }

    public RefreshInicialFalhouException(int status, string corpoBruto, string mensagem) : base(mensagem)
    {
        StatusHttp = status;
        CorpoBruto = corpoBruto;
    }
}
