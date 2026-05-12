namespace PcaImporter.Application.Logs;

public interface IRegistroLogs
{
    void Registrar(NivelLog nivel, string categoria, string mensagem, string? detalhes = null);

    PaginaLogsDto Consultar(int pagina, int tamanhoPagina, NivelLog? nivelMinimo = null, string? categoria = null);
}
