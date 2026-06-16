namespace PcaImporter.Infrastructure.Persistencia.Entidades;

public sealed class ListaLinkEntity
{
    public int Id { get; set; }
    public string? Rotulo { get; set; }
    public string Url { get; set; } = string.Empty;
    public string IdPlanilha { get; set; } = string.Empty;

    /// <summary>"INSUMOS" | "SANEANTES" | "MEDICAMENTOS" (livre — UI guia).</summary>
    public string? Classe { get; set; }

    /// <summary>Numeração contínua dentro da classe. Reinicia em 1 por classe.</summary>
    public int? NumeroGrupo { get; set; }

    /// <summary>pendente | validando | valido | invalido | erro</summary>
    public string Estado { get; set; } = "pendente";
    public int? TotalMateriais { get; set; }
    public string? MensagemErro { get; set; }
    public DateTimeOffset? ValidadoEm { get; set; }

    /// <summary>Descrição da contratação lida da aba DFD da planilha (campo "descricao").</summary>
    public string? Descricao { get; set; }

    /// <summary>Preenchido quando uma importação a partir deste link foi concluída com sucesso.</summary>
    public DateTimeOffset? ImportadoEm { get; set; }

    /// <summary>Id da última execução de importação disparada por este link.</summary>
    public string? UltimoIdExecucao { get; set; }

    /// <summary>Mensagem do erro da última tentativa de importação que falhou (nula quando a última tentativa foi sucesso).</summary>
    public string? UltimoErroImportacao { get; set; }

    public DateTimeOffset CriadoEm { get; set; }
    public string? CriadoPorLogin { get; set; }

    /// <summary>NULL = ativo; preenchido = está na lixeira.</summary>
    public DateTimeOffset? ExcluidoEm { get; set; }
    public string? ExcluidoPorLogin { get; set; }

    public List<ListaItemEntity> Itens { get; set; } = new();
}
