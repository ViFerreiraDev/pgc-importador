using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Importacao;
using PcaImporter.Application.Validacao;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/lista-validacao")]
[Authorize]
public sealed class ValidacaoController : ControllerBase
{
    private readonly IServicoListaValidacao _servico;
    private readonly IServicoImportacao _importacao;

    public ValidacaoController(IServicoListaValidacao servico, IServicoImportacao importacao)
    {
        _servico = servico;
        _importacao = importacao;
    }

    [HttpGet]
    public async Task<ActionResult<ListaValidacaoDto>> Obter(CancellationToken ct)
    {
        return Ok(await _servico.ObterListaAsync(ct));
    }

    [HttpPost("links")]
    public async Task<ActionResult<LinkValidacaoDto>> AdicionarLink(
        [FromBody] AdicionarLinkInput corpo, CancellationToken ct)
    {
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.Url))
        {
            return BadRequest(new { erro = "URL obrigatória." });
        }
        try
        {
            var dto = await _servico.AdicionarLinkAsync(corpo.Url, corpo.Rotulo, corpo.Classe, corpo.NumeroGrupo, User.Identity?.Name, ct);
            return Ok(dto);
        }
        catch (LinkJaCadastradoException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpPost("links/extrair")]
    public async Task<ActionResult<IReadOnlyList<LinkValidacaoDto>>> ExtrairLinks(
        [FromBody] ExtrairLinksInput corpo, CancellationToken ct)
    {
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.Texto))
        {
            return Ok(Array.Empty<LinkValidacaoDto>());
        }
        if (corpo.Texto.Length > 51_200)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { erro = "Texto colado excede 50 KB." });
        }
        var lista = await _servico.AdicionarLinksDoTextoAsync(corpo.Texto, corpo.Classe, corpo.DetectarNumeroGrupoNoTexto, User.Identity?.Name, ct);
        return Ok(lista);
    }

    /// <summary>
    /// Compara texto colado com a lista ativa: adiciona os novos, retorna 3 listas
    /// (adicionados, duplicados, ausentes). Não exclui nada — só sinaliza ausentes.
    /// </summary>
    [HttpPost("links/comparar")]
    public async Task<ActionResult<DiffLoteDto>> CompararLote(
        [FromBody] ExtrairLinksInput corpo, CancellationToken ct)
    {
        if (corpo is null || string.IsNullOrWhiteSpace(corpo.Texto))
        {
            return Ok(new DiffLoteDto(Array.Empty<LinkValidacaoDto>(), Array.Empty<DuplicadoLoteDto>(), Array.Empty<AusenteLoteDto>()));
        }
        if (corpo.Texto.Length > 51_200)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new { erro = "Texto colado excede 50 KB." });
        }
        var diff = await _servico.CompararLoteAsync(corpo.Texto, corpo.Classe, corpo.DetectarNumeroGrupoNoTexto, User.Identity?.Name, ct);
        return Ok(diff);
    }

    [HttpPost("links/{id:int}/validar")]
    public async Task<ActionResult<LinkValidacaoDto>> Validar(int id, CancellationToken ct)
    {
        try
        {
            var dto = await _servico.ValidarLinkAsync(id, ct);
            return Ok(dto);
        }
        catch (LinkNaoEncontradoException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    [HttpDelete("links/{id:int}")]
    public async Task<ActionResult> Excluir(int id, CancellationToken ct)
    {
        try
        {
            await _servico.ExcluirLinkAsync(id, User.Identity?.Name, ct);
            return NoContent();
        }
        catch (LinkNaoEncontradoException)
        {
            return NotFound();
        }
    }

    [HttpPost("links/{id:int}/restaurar")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Restaurar(int id, CancellationToken ct)
    {
        try
        {
            await _servico.RestaurarLinkAsync(id, User.Identity?.Name, ct);
            return NoContent();
        }
        catch (LinkNaoEncontradoException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { erro = ex.Message });
        }
    }

    [HttpDelete("lixeira/{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ApagarDefinitivamente(int id, CancellationToken ct)
    {
        await _servico.ApagarDefinitivamenteAsync(id, User.Identity?.Name, ct);
        return NoContent();
    }

    [HttpPost("itens/{id:int}/revisar")]
    public async Task<ActionResult<IReadOnlyList<RevisorDto>>> Revisar(int id, CancellationToken ct)
    {
        var login = User.Identity?.Name;
        if (string.IsNullOrEmpty(login)) return Unauthorized();
        var revisores = await _servico.RevisarItemAsync(id, login, ct);
        return Ok(revisores);
    }

    [HttpDelete("itens/{id:int}/revisar")]
    public async Task<ActionResult<IReadOnlyList<RevisorDto>>> Desrevisar(int id, CancellationToken ct)
    {
        var login = User.Identity?.Name;
        if (string.IsNullOrEmpty(login)) return Unauthorized();
        var revisores = await _servico.DesrevisarItemAsync(id, login, ct);
        return Ok(revisores);
    }

    /// <summary>
    /// Dispara importação de um link da lista. Repassa pro ServicoImportacao.IniciarLinkAsync,
    /// que aplica todas as regras (duplicado, etc) e retorna o id da execução.
    /// </summary>
    [HttpPost("links/{id:int}/importar")]
    public async Task<ActionResult<object>> Importar(int id, [FromQuery] bool confirmarDuplicado, CancellationToken ct)
    {
        var lista = await _servico.ObterListaAsync(ct);
        var link = lista.Ativos.FirstOrDefault(l => l.Id == id);
        if (link is null) return NotFound(new { erro = "Link não encontrado na lista ativa." });

        try
        {
            var idExec = await _importacao.IniciarLinkAsync(link.Url, confirmarDuplicado, User.Identity?.Name, ct);
            return Ok(new { id = idExec });
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
}
