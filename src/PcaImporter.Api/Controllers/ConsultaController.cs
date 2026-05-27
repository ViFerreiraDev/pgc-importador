using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Consulta;

namespace PcaImporter.Api.Controllers;

/// <summary>
/// Endpoints públicos de consulta que reutilizam o token Compras mantido vivo
/// pelo servidor (refresh automático via KeepAliveTokenWorker).
/// Liberados para CORS de qualquer origem — pensados para integrações externas
/// (planilhas, scripts internos, automações de bastidor).
/// </summary>
[ApiController]
[Route("api/consulta")]
[AllowAnonymous]
[EnableCors(ConsultaCors.PolicyName)]
public sealed class ConsultaController : ControllerBase
{
    private readonly IConsultaPessoaClient _consulta;

    public ConsultaController(IConsultaPessoaClient consulta)
    {
        _consulta = consulta;
    }

    /// <summary>
    /// Resolve um CPF para o nome cadastrado na RFB via gateway Serpro
    /// (mesmo serviço usado pela tela de DFD).
    /// </summary>
    /// <param name="cpf">CPF — aceita com ou sem máscara, qualquer não-dígito é removido.</param>
    /// <param name="artefatoId">Opcional. Sobrescreve o artefato padrão.</param>
    /// <returns>Nome em texto plano (text/plain) em caso de sucesso; JSON com erro caso contrário.</returns>
    [HttpGet("nome-por-cpf")]
    [Produces("text/plain", "application/json")]
    public async Task<IActionResult> NomePorCpf(
        [FromQuery] string cpf,
        [FromQuery] int? artefatoId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return BadRequest(new { erro = "Parâmetro cpf é obrigatório." });
        }

        var resultado = await _consulta.ObterNomePorCpfAsync(cpf, artefatoId, ct);
        if (resultado.Sucesso && !string.IsNullOrEmpty(resultado.Nome))
        {
            return Content(resultado.Nome, "text/plain; charset=utf-8");
        }

        return StatusCode(resultado.StatusHttp == 0 ? 502 : resultado.StatusHttp,
            new { erro = resultado.Erro ?? "Falha desconhecida." });
    }
}

public static class ConsultaCors
{
    public const string PolicyName = "ConsultaAnyOrigin";
}
