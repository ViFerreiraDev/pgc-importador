namespace PcaImporter.Application.Compras.Catalogo;

public interface IComprasGovCatalogoClient
{
    Task<ItemCatalogoDto?> ConsultarMaterialAsync(string codigo, CancellationToken ct = default);

    Task<ItemCatalogoDto?> ConsultarServicoAsync(string codigo, CancellationToken ct = default);
}

public sealed class CatalogoHttpException : Exception
{
    public int StatusHttp { get; }
    public string CorpoBruto { get; }

    public CatalogoHttpException(int status, string corpoBruto, string mensagem) : base(mensagem)
    {
        StatusHttp = status;
        CorpoBruto = corpoBruto;
    }
}
