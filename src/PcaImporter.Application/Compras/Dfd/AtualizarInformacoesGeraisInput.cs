namespace PcaImporter.Application.Compras.Dfd;

public sealed record AtualizarInformacoesGeraisInput(
    DateOnly DataConclusaoContratacao,
    string Objeto,
    NivelPrioridade NivelPrioridade,
    string? JustificativaEmergencial,
    string? JustificativaPrioridade
);

public enum NivelPrioridade
{
    BAIXO = 0,
    MEDIO = 1,
    ALTO = 2,
}

public sealed record InformacoesGeraisAtualizadasDto(
    long Id,
    long IdArtefato,
    string? Objeto,
    DateTimeOffset? DataPrevista,
    NivelPrioridade NivelPrioridade,
    bool Emergencial,
    string? JustificativaEmergencial,
    decimal? ValorTotalEstimado,
    string CorpoBruto
);
