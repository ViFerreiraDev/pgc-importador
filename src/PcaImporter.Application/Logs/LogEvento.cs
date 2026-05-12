namespace PcaImporter.Application.Logs;

public sealed record LogEvento(
    long Id,
    DateTimeOffset OcorridoEm,
    NivelLog Nivel,
    string Categoria,
    string Mensagem,
    string? Detalhes
);

public enum NivelLog
{
    Info = 0,
    Sucesso = 1,
    Aviso = 2,
    Erro = 3,
}

public sealed record PaginaLogsDto(
    IReadOnlyList<LogEvento> Itens,
    int Pagina,
    int TamanhoPagina,
    int Total
);
