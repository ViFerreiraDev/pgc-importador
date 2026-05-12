namespace PcaImporter.Application.Importacao;

public enum EstadoImportacao
{
    Pendente = 0,
    Executando = 1,
    Concluida = 2,
    Falhou = 3,
    Cancelada = 4,
}

public sealed record StatusImportacao(
    string Id,
    EstadoImportacao Estado,
    DateTimeOffset IniciadaEm,
    DateTimeOffset? ConcluidaEm,
    int TotalEtapas,
    int EtapasConcluidas,
    string? EtapaAtual,
    long? IdArtefatoCriado,
    long? IdFormalizacaoCriado,
    int? NumeroDfd,
    int? AnoDfd,
    int TotalMateriais,
    int MateriaisAdicionados,
    string? UltimoErro,
    IReadOnlyList<EventoImportacao> Eventos
);

public sealed record EventoImportacao(
    DateTimeOffset OcorridoEm,
    string Tipo,
    string Mensagem,
    string? Detalhe = null
);
