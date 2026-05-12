using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Catalogo;
using PcaImporter.Application.Compras.Catalogo;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/catalogo")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class CatalogoController : ControllerBase
{
    private readonly IComprasGovCatalogoClient _client;
    private readonly IServicoSincronizacaoCatalogo _sinc;
    private readonly ILogger<CatalogoController> _log;

    public CatalogoController(
        IComprasGovCatalogoClient client,
        IServicoSincronizacaoCatalogo sinc,
        ILogger<CatalogoController> log)
    {
        _client = client;
        _sinc = sinc;
        _log = log;
    }

    [HttpGet("status")]
    public async Task<ActionResult<StatusSincronizacaoCatalogo>> Status(CancellationToken ct)
    {
        return Ok(await _sinc.ObterStatusAsync(ct));
    }

    [HttpPost("sincronizar")]
    public ActionResult<object> Sincronizar()
    {
        var iniciou = _sinc.Iniciar();
        if (!iniciou)
        {
            return Conflict(new { erro = "Sincronização já está em andamento." });
        }
        return Accepted(new { iniciado = true });
    }

    [HttpPost("cancelar")]
    public ActionResult<object> Cancelar()
    {
        var ok = _sinc.Cancelar();
        return Ok(new { cancelado = ok });
    }

    [HttpDelete]
    public async Task<ActionResult<object>> Limpar(CancellationToken ct)
    {
        var removidos = await _sinc.LimparAsync(ct);
        return Ok(new { removidos });
    }

    [HttpGet("material/{codigo}")]
    public async Task<ActionResult<ItemCatalogoDto>> ConsultarMaterial(string codigo, CancellationToken ct)
    {
        try
        {
            var item = await _client.ConsultarMaterialAsync(codigo, ct);
            if (item is null)
            {
                return NotFound(new { erro = $"Material {codigo} nao encontrado." });
            }
            return Ok(item);
        }
        catch (CatalogoHttpException ex)
        {
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp == 0 ? 502 : ex.StatusHttp, title: "Falha no catalogo", instance: ex.CorpoBruto);
        }
    }

    [HttpGet("servico/{codigo}")]
    public async Task<ActionResult<ItemCatalogoDto>> ConsultarServico(string codigo, CancellationToken ct)
    {
        try
        {
            var item = await _client.ConsultarServicoAsync(codigo, ct);
            if (item is null)
            {
                return NotFound(new { erro = $"Servico {codigo} nao encontrado." });
            }
            return Ok(item);
        }
        catch (CatalogoHttpException ex)
        {
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp == 0 ? 502 : ex.StatusHttp, title: "Falha no catalogo", instance: ex.CorpoBruto);
        }
    }
}
