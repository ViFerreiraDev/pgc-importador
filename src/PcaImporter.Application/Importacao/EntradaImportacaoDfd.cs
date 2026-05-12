using PcaImporter.Application.Compras.Dfd;

namespace PcaImporter.Application.Importacao;

public sealed class EntradaImportacaoDfd
{
    public DateOnly? DataConclusaoContratacao { get; set; }
    public string? DataConclusaoContratacaoTexto { get; set; }

    public string? Descricao { get; set; }

    public NivelPrioridade NivelPrioridade { get; set; } = NivelPrioridade.BAIXO;
    public string? NivelPrioridadeTexto { get; set; }

    public string? JustificativaPrioridade { get; set; }
    public string? JustificativaNecessidade { get; set; }

    public string? ResponsavelCpf { get; set; }
    public string? ResponsavelNome { get; set; }
    public string? ResponsavelEmail { get; set; }
    public string? ResponsavelCargo { get; set; }

    public List<EntradaImportacaoMaterial> Materiais { get; set; } = new();
}

public sealed class EntradaImportacaoMaterial
{
    public int LinhaPlanilha { get; set; }
    public string Tipo { get; set; } = "MATERIAL";

    public string? Codigo { get; set; }

    public decimal? Quantidade { get; set; }
    public string? QuantidadeTexto { get; set; }

    public decimal? ValorUnitario { get; set; }
    public string? ValorUnitarioTexto { get; set; }

    public string? SiglaUnidadeFornecimento { get; set; }
    public string Moeda { get; set; } = "Real";
}
