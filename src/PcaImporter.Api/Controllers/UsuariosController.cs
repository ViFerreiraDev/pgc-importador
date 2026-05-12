using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Usuarios;
using PcaImporter.Domain.Usuarios;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public sealed class UsuariosController : ControllerBase
{
    private readonly IServicoUsuarios _servico;

    public UsuariosController(IServicoUsuarios servico)
    {
        _servico = servico;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UsuarioDto>>> Listar(CancellationToken ct)
    {
        return Ok(await _servico.ListarAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<UsuarioDto>> Criar([FromBody] CriarUsuarioInput corpo, CancellationToken ct)
    {
        // Apenas admins podem criar admins. Usuários normais só podem criar outros normais.
        var ehAdmin = User.IsInRole("Admin");
        if (corpo.Papel == Papel.Admin && !ehAdmin)
        {
            return Forbid();
        }
        try
        {
            var criado = await _servico.CriarAsync(corpo, User.Identity?.Name, ct);
            return CreatedAtAction(nameof(Listar), new { id = criado.Id }, criado);
        }
        catch (UsuarioJaExisteException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Remover(int id, CancellationToken ct)
    {
        var ok = await _servico.RemoverAsync(id, ct);
        if (!ok) return BadRequest(new { erro = "Não foi possível remover (usuário inexistente ou último admin)." });
        return NoContent();
    }

    [HttpPost("{id:int}/senha")]
    public async Task<ActionResult> AlterarSenha(int id, [FromBody] AlterarSenhaInput corpo, CancellationToken ct)
    {
        // Usuário só pode alterar a própria senha; admin pode alterar de qualquer um.
        var ehAdmin = User.IsInRole("Admin");
        var meuId = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var mid) ? mid : 0;
        if (!ehAdmin && meuId != id) return Forbid();

        var ok = await _servico.AlterarSenhaAsync(id, corpo.NovaSenha, ct);
        if (!ok) return BadRequest(new { erro = "Não foi possível alterar a senha." });
        return NoContent();
    }
}
