using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Token;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/token")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class TokenController : ControllerBase
{
    private readonly IGerenciadorTokenSessao _gerenciador;
    private readonly ILogger<TokenController> _log;

    public TokenController(IGerenciadorTokenSessao gerenciador, ILogger<TokenController> log)
    {
        _gerenciador = gerenciador;
        _log = log;
    }

    [HttpGet("status")]
    public ActionResult<StatusTokenDto> ObterStatus()
    {
        return Ok(_gerenciador.ObterStatus());
    }

    [HttpPost]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<ActionResult<StatusTokenDto>> Definir([FromBody] DefinirTokenInput input, CancellationToken ct)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.RefreshToken))
        {
            return BadRequest(new { erro = "Campo 'refreshToken' obrigatorio." });
        }

        try
        {
            var status = await _gerenciador.DefinirAPartirDoRefreshAsync(input.RefreshToken, ct);
            return Ok(status);
        }
        catch (FormatException ex)
        {
            _log.LogWarning(ex, "Refresh token invalido.");
            return BadRequest(new { erro = ex.Message });
        }
        catch (RefreshInicialFalhouException ex)
        {
            _log.LogWarning(ex, "Bootstrap a partir do refresh falhou.");
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp == 0 ? 502 : ex.StatusHttp, title: "Refresh inicial falhou", instance: ex.CorpoBruto);
        }
    }

    [HttpPost("refresh")]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public async Task<ActionResult<StatusTokenDto>> ForcarRefresh(CancellationToken ct)
    {
        var status = await _gerenciador.ForcarRefreshAsync(ct);
        return Ok(status);
    }

    [HttpDelete]
    [Microsoft.AspNetCore.Authorization.Authorize(Roles = "Admin")]
    public ActionResult<StatusTokenDto> Limpar()
    {
        _gerenciador.Limpar();
        return Ok(_gerenciador.ObterStatus());
    }
}
