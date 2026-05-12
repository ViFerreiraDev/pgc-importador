using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Logs;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/logs")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class LogsController : ControllerBase
{
    private readonly IRegistroLogs _logs;

    public LogsController(IRegistroLogs logs)
    {
        _logs = logs;
    }

    [HttpGet]
    public ActionResult<PaginaLogsDto> Listar(
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 50,
        [FromQuery] NivelLog? nivel = null,
        [FromQuery] string? categoria = null)
    {
        return Ok(_logs.Consultar(pagina, tamanhoPagina, nivel, string.IsNullOrWhiteSpace(categoria) ? null : categoria));
    }
}
