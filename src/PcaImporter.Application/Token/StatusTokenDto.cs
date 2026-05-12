using PcaImporter.Domain.Token;

namespace PcaImporter.Application.Token;

public sealed record StatusTokenDto(
    EstadoToken Estado,
    DateTimeOffset? EmitidoEm,
    DateTimeOffset? ExpiraEm,
    long? SegundosRestantes,
    string? Sub,
    long? IdSessao,
    int? NumeroUasg,
    IReadOnlyList<string>? Mnemonicos,
    DateTimeOffset? UltimoRefreshEm,
    string? UltimoErroRefresh,
    bool TemRefreshToken
);
