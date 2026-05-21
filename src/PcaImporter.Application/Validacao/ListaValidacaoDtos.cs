namespace PcaImporter.Application.Validacao;

public sealed record ListaValidacaoDto(
    IReadOnlyList<LinkValidacaoDto> Ativos,
    IReadOnlyList<LinkValidacaoDto> Lixeira,
    IReadOnlyList<GapClasseDto> Gaps
);

/// <summary>Lacunas detectadas na sequência de NumeroGrupo dentro de uma classe.</summary>
public sealed record GapClasseDto(
    string Classe,
    int? UltimoNumero,
    IReadOnlyList<int> Faltantes
);

public sealed record LinkValidacaoDto(
    int Id,
    string? Rotulo,
    string Url,
    string IdPlanilha,
    string? Classe,
    int? NumeroGrupo,
    string Estado,
    int? TotalMateriais,
    string? MensagemErro,
    DateTimeOffset? ValidadoEm,
    DateTimeOffset CriadoEm,
    string? CriadoPorLogin,
    DateTimeOffset? ExcluidoEm,
    string? ExcluidoPorLogin,
    IReadOnlyList<ItemValidacaoDto> Erros,
    IReadOnlyList<ItemValidacaoDto> Divergencias,
    IReadOnlyList<AvisoListaDto> Avisos
);

public sealed record ItemValidacaoDto(
    int Id,
    string Tipo,
    string Fingerprint,
    string? Local,
    string? Campo,
    int? Linha,
    string? Codigo,
    double? DeltaPct,
    string Mensagem,
    /// <summary>JSON cru com o payload completo (para o cliente expandir conforme tipo)</summary>
    string PayloadJson,
    DateTimeOffset CriadoEm,
    IReadOnlyList<RevisorDto> Revisores
);

public sealed record AvisoListaDto(
    string Local,
    int? Linha,
    string Mensagem
);

public sealed record RevisorDto(
    string Login,
    DateTimeOffset RevisadoEm
);

public sealed record AdicionarLinkInput(
    string Url,
    string? Rotulo,
    string? Classe = null,
    int? NumeroGrupo = null
);

public sealed record ExtrairLinksInput(
    string Texto,
    /// <summary>Classe alvo dos links extraídos (ex.: "INSUMOS"). Obrigatória pra registrar grupo.</summary>
    string? Classe = null,
    /// <summary>Se true, parser tenta detectar "Grupo X" no texto e usa X como numeroGrupo. Senão, atribui sequencialmente após o último.</summary>
    bool DetectarNumeroGrupoNoTexto = true
);

/// <summary>
/// Resumo do diff entre o texto colado e a lista atual ativa.
/// </summary>
public sealed record DiffLoteDto(
    /// <summary>Novos links que foram efetivamente adicionados.</summary>
    IReadOnlyList<LinkValidacaoDto> Adicionados,
    /// <summary>URLs no texto que já existiam na lista ativa (não foram adicionadas).</summary>
    IReadOnlyList<DuplicadoLoteDto> Duplicados,
    /// <summary>Links que estão na lista ativa mas NÃO apareceram no texto colado.</summary>
    IReadOnlyList<AusenteLoteDto> Ausentes
);

public sealed record DuplicadoLoteDto(
    int LinkExistenteId,
    string IdPlanilha,
    string? RotuloColado,
    string? RotuloExistente,
    string Url
);

public sealed record AusenteLoteDto(
    int LinkId,
    string IdPlanilha,
    string? Rotulo,
    string Url,
    string Estado
);
