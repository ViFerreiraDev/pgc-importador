using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Usuarios;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IServicoUsuarios _servico;

    public AuthController(IServicoUsuarios servico)
    {
        _servico = servico;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<UsuarioDto>> Login([FromBody] LoginInput corpo, CancellationToken ct)
    {
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.Login) || string.IsNullOrWhiteSpace(corpo.Senha))
        {
            return BadRequest(new { erro = "Login e senha são obrigatórios." });
        }

        var u = await _servico.AutenticarAsync(corpo.Login, corpo.Senha, ct);
        if (u is null)
        {
            return Unauthorized(new { erro = "Login ou senha inválidos." });
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.Id.ToString()),
            new(ClaimTypes.Name, u.Login),
            new("nome", u.Nome),
            new(ClaimTypes.Role, u.Papel.ToString()),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true });

        return Ok(u);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<ActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UsuarioDto>> Me(CancellationToken ct)
    {
        var login = User.Identity?.Name;
        if (string.IsNullOrEmpty(login)) return Unauthorized();
        var u = await _servico.ObterPorLoginAsync(login, ct);
        if (u is null) return Unauthorized();
        return Ok(u);
    }
}

public sealed record LoginInput(string Login, string Senha);
