using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Importacao;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/importacao")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class ImportacaoController : ControllerBase
{
    private readonly IServicoImportacao _servico;

    public ImportacaoController(IServicoImportacao servico)
    {
        _servico = servico;
    }

    [HttpPost("validar")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ResultadoValidacaoImportacao>> Validar(IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo obrigatório (campo 'arquivo')." });
        }
        if (arquivo.Length > 20 * 1024 * 1024)
        {
            return BadRequest(new { erro = "Arquivo maior que 20 MB." });
        }
        await using var stream = arquivo.OpenReadStream();
        var resultado = await _servico.ValidarAsync(stream, ct);
        return Ok(resultado with { Entrada = null });
    }

    [HttpPost("iniciar")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<object>> Iniciar(IFormFile arquivo, CancellationToken ct)
    {
        if (arquivo is null || arquivo.Length == 0)
        {
            return BadRequest(new { erro = "Arquivo obrigatório (campo 'arquivo')." });
        }
        await using var stream = arquivo.OpenReadStream();
        try
        {
            var id = await _servico.IniciarAsync(stream, ct);
            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPost("validar-link")]
    public async Task<ActionResult<object>> ValidarLink(
        [FromBody] LinkPlanilhaApi corpo, CancellationToken ct)
    {
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.Url))
        {
            return BadRequest(new { erro = "Campo 'url' obrigatório." });
        }
        try
        {
            var resultado = await _servico.ValidarLinkAsync(corpo.Url.Trim(), ct);
            var anterior = await _servico.ConsultarHistoricoPorLinkAsync(corpo.Url.Trim(), ct);
            return Ok(new
            {
                resultado.Valido,
                resultado.TotalMateriais,
                resultado.Erros,
                resultado.Avisos,
                Duplicado = anterior is not null,
                Anterior = anterior,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPost("iniciar-link")]
    public async Task<ActionResult<object>> IniciarLink(
        [FromBody] IniciarLinkApi corpo, CancellationToken ct)
    {
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.Url))
        {
            return BadRequest(new { erro = "Campo 'url' obrigatório." });
        }
        try
        {
            var id = await _servico.IniciarLinkAsync(corpo.Url.Trim(), corpo.ConfirmarDuplicado, User.Identity?.Name, ct);
            return Ok(new { id });
        }
        catch (ImportacaoDuplicadaException ex)
        {
            return Conflict(new
            {
                erro = ex.Message,
                duplicado = true,
                anterior = ex.Anterior,
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public ActionResult<StatusImportacao> Status(string id)
    {
        var status = _servico.ObterStatus(id);
        if (status is null) return NotFound(new { erro = $"Importação '{id}' não encontrada." });
        return Ok(status);
    }

    [HttpGet("recentes")]
    public ActionResult<IReadOnlyList<StatusImportacao>> Recentes([FromQuery] int limite = 20)
    {
        return Ok(_servico.ListarRecentes(limite));
    }

    [HttpGet("metricas")]
    public async Task<ActionResult<MetricasImportacaoDto>> Metricas(CancellationToken ct)
    {
        var m = await _servico.ObterMetricasAsync(ct);
        return Ok(m);
    }

    [HttpGet("historico")]
    public async Task<ActionResult<IReadOnlyList<HistoricoImportacaoDto>>> Historico(
        [FromQuery] int limite = 200, CancellationToken ct = default)
    {
        return Ok(await _servico.ListarHistoricoAsync(limite, ct));
    }

    [HttpDelete("metricas")]
    public async Task<ActionResult<object>> ResetarMetricas(CancellationToken ct)
    {
        var removidos = await _servico.ResetarMetricasAsync(ct);
        return Ok(new { removidos });
    }
}

public sealed record LinkPlanilhaApi(string Url);
public sealed record IniciarLinkApi(string Url, bool ConfirmarDuplicado = false);
