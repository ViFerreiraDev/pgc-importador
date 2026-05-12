namespace PcaImporter.Domain.Token;

public sealed record TokenSessao(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset EmitidoEm,
    DateTimeOffset ExpiraEm,
    string Sub,
    long IdSessao,
    int NumeroUasg,
    IReadOnlyList<string> Mnemonicos,
    string TipoAutenticacao
)
{
    public TimeSpan TempoRestante(DateTimeOffset agora) => ExpiraEm - agora;

    public bool EstaExpirado(DateTimeOffset agora) => agora >= ExpiraEm;
}
