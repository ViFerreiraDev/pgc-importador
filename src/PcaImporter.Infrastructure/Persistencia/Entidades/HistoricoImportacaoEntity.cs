namespace PcaImporter.Infrastructure.Persistencia.Entidades;

public sealed class HistoricoImportacaoEntity
{
    public int Id { get; set; }
    public string IdPlanilha { get; set; } = string.Empty;
    public string UrlOriginal { get; set; } = string.Empty;
    public DateTimeOffset ImportadaEm { get; set; }
    public string IdExecucao { get; set; } = string.Empty;
    public int NumeroDfd { get; set; }
    public int AnoDfd { get; set; }
    public long IdArtefato { get; set; }
    public long IdFormalizacaoDemanda { get; set; }
    public int TotalMateriais { get; set; }
    public decimal ValorTotal { get; set; }
    public bool Sucesso { get; set; }
    public string? MensagemErro { get; set; }
    public int? LinhaErro { get; set; }
    public string? Descricao { get; set; }
    public string? UsuarioLogin { get; set; }
}
