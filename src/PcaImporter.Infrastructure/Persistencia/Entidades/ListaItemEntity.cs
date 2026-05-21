namespace PcaImporter.Infrastructure.Persistencia.Entidades;

public sealed class ListaItemEntity
{
    public int Id { get; set; }
    public int LinkId { get; set; }
    public ListaLinkEntity Link { get; set; } = null!;

    /// <summary>"erro" ou "divergencia". Avisos não persistem (não revisáveis).</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Hash estável da identidade lógica do item (sobrevive a re-validações).</summary>
    public string Fingerprint { get; set; } = string.Empty;
    public int FingerprintVersao { get; set; } = 1;

    // Colunas tipadas pra WHERE/ORDER BY direto (filtros da UI).
    public string? Local { get; set; }
    public string? Campo { get; set; }
    public int? Linha { get; set; }
    public string? Codigo { get; set; }
    public double? DeltaPct { get; set; }

    /// <summary>JSON com payload completo (mensagem, valores, sigla ref, etc).</summary>
    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CriadoEm { get; set; }

    public List<ListaItemRevisaoEntity> Revisoes { get; set; } = new();
}
