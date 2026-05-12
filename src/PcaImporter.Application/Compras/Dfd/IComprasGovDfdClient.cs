namespace PcaImporter.Application.Compras.Dfd;

public interface IComprasGovDfdClient
{
    Task<DfdCriadoDto> CriarDfdAsync(CancellationToken ct = default);

    Task<MaterialServicoCriadoDto> AdicionarMaterialServicoAsync(MaterialServicoInput input, CancellationToken ct = default);

    Task<InformacoesGeraisAtualizadasDto> AtualizarInformacoesGeraisAsync(
        long idFormalizacaoDemanda,
        long idArtefato,
        int uasg,
        int numero,
        int ano,
        AtualizarInformacoesGeraisInput input,
        CancellationToken ct = default);

    Task<JustificativaAtualizadaDto> AtualizarJustificativaNecessidadeAsync(
        long idSecao,
        long idItem,
        string textoSimples,
        CancellationToken ct = default);

    Task<ResponsavelCriadoDto> AdicionarResponsavelAsync(
        long idArtefato,
        int numero,
        int ano,
        int ordem,
        AdicionarResponsavelInput input,
        CancellationToken ct = default);
}

public sealed record JustificativaAtualizadaDto(
    long Id,
    long IdSecao,
    string Conteudo,
    DateTimeOffset DataHoraOperacao,
    string CorpoBruto
);

public sealed class ComprasGovHttpException : Exception
{
    public int StatusHttp { get; }
    public string CorpoBruto { get; }

    public ComprasGovHttpException(int status, string corpoBruto, string mensagem) : base(mensagem)
    {
        StatusHttp = status;
        CorpoBruto = corpoBruto;
    }
}
