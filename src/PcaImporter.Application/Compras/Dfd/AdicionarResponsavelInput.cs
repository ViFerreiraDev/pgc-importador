namespace PcaImporter.Application.Compras.Dfd;

public sealed record AdicionarResponsavelInput(
    string Cpf,
    string Nome,
    string Email,
    string Cargo
);

public sealed record ResponsavelCriadoDto(
    long? Id,
    long IdArtefato,
    string Cpf,
    string Nome,
    string Email,
    int IdCargo,
    string Cargo,
    string Instrumento,
    bool AssinaDocumento,
    int Ordem,
    string CorpoBruto
);
