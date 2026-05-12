namespace PcaImporter.Application.Compras.Dfd;

public sealed record DfdCriadoDto(
    long IdArtefato,
    long IdFormalizacaoDemanda,
    int Numero,
    int Ano,
    int Uasg,
    string NomeUasg,
    string Status,
    int? AnoPca,
    string? StatusPca,
    DateTimeOffset CriadoEm,
    SecoesDfd Secoes,
    string CorpoBruto
);

public sealed record SecoesDfd(
    long IdSecaoPrincipal,
    long? IdItemInformacoesGerais,
    long? IdItemJustificativaNecessidade,
    long? IdItemMateriaisServicos,
    long? IdItemResponsaveis,
    long? IdItemAcompanhamento
);
