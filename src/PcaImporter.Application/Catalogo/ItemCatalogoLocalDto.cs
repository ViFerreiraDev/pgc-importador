namespace PcaImporter.Application.Catalogo;

public sealed record ItemCatalogoLocalDto(
    int CodigoItem,
    int CodigoGrupo,
    string NomeGrupo,
    int CodigoClasse,
    string NomeClasse,
    int CodigoPdm,
    string NomePdm,
    string DescricaoItem,
    bool StatusItem,
    bool ItemSustentavel,
    string? CodigoNcm,
    DateTimeOffset? AtualizadoEmFonte,
    DateTimeOffset SincronizadoEm
);

public interface IRepositorioCatalogoMaterial
{
    Task<int> ContarAsync(CancellationToken ct = default);

    Task<DateTimeOffset?> UltimaSincronizacaoAsync(CancellationToken ct = default);

    Task<ItemCatalogoLocalDto?> BuscarPorCodigoAsync(int codigoItem, CancellationToken ct = default);

    Task UpsertEmLoteAsync(IReadOnlyCollection<ItemCatalogoLocalDto> itens, CancellationToken ct = default);

    Task<int> LimparAsync(CancellationToken ct = default);
}
