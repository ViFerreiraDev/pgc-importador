namespace PcaImporter.Infrastructure.Persistencia.Entidades;

public sealed class ListaItemRevisaoEntity
{
    public int ItemId { get; set; }
    public ListaItemEntity Item { get; set; } = null!;

    public string RevisadoPorLogin { get; set; } = string.Empty;
    public DateTimeOffset RevisadoEm { get; set; }
}
