namespace PcaImporter.Infrastructure.Compras;

public sealed class ComprasGovOptions
{
    public const string SecaoConfig = "ComprasGov";

    public string BaseUrl { get; set; } = "https://cnetmobile.estaleiro.serpro.gov.br";

    public string BaseUrlDadosAbertos { get; set; } = "https://dadosabertos.compras.gov.br";

    public TokenOptions Token { get; set; } = new();

    public AreaRequisitantePadraoOptions AreaRequisitantePadrao { get; set; } = new();

    public sealed class AreaRequisitantePadraoOptions
    {
        public int IdArea { get; set; } = 41648;
        public string NomeArea { get; set; } = "SMS";
        public int Permissao { get; set; } = 1;
        public bool PossuoPermissaoAdmin { get; set; } = false;
    }

    public ResponsavelPadraoOptions ResponsavelPadrao { get; set; } = new();

    public sealed class ResponsavelPadraoOptions
    {
        public int IdCargo { get; set; } = 8;
        public string Instrumento { get; set; } = "PORTARIA";
        public bool AssinaDocumento { get; set; } = true;
    }

    public sealed class TokenOptions
    {
        public string CaminhoRetoken { get; set; } = "/comprasnet-usuario/v2/sessao/governo/retoken";

        public int LimiarRefreshSegundos { get; set; } = 300;

        public int IntervaloKeepAliveSegundos { get; set; } = 30;

        public int TimeoutHttpSegundos { get; set; } = 30;

        public int BackoffMinimoSegundos { get; set; } = 5;

        public int BackoffMaximoSegundos { get; set; } = 60;
    }
}
