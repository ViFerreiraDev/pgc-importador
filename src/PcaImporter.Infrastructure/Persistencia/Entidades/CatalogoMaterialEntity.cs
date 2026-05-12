namespace PcaImporter.Infrastructure.Persistencia.Entidades;

public sealed class CatalogoMaterialEntity
{
    public int CodigoItem { get; set; }
    public int CodigoGrupo { get; set; }
    public string NomeGrupo { get; set; } = string.Empty;
    public int CodigoClasse { get; set; }
    public string NomeClasse { get; set; } = string.Empty;
    public int CodigoPdm { get; set; }
    public string NomePdm { get; set; } = string.Empty;
    public string DescricaoItem { get; set; } = string.Empty;
    public bool StatusItem { get; set; }
    public bool ItemSustentavel { get; set; }
    public string? CodigoNcm { get; set; }
    public DateTimeOffset? AtualizadoEmFonte { get; set; }
    public DateTimeOffset SincronizadoEm { get; set; }
}
