using Microsoft.AspNetCore.Mvc;
using PcaImporter.Application.Compras.Dfd;
using PcaImporter.Application.Logs;
using PcaImporter.Application.Token;

namespace PcaImporter.Api.Controllers;

[ApiController]
[Route("api/dfd")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class DfdController : ControllerBase
{
    private readonly IComprasGovDfdClient _client;
    private readonly IRegistroDfdAtual _registro;
    private readonly IRegistroLogs _logs;
    private readonly ILogger<DfdController> _log;

    public DfdController(IComprasGovDfdClient client, IRegistroDfdAtual registro, IRegistroLogs logs, ILogger<DfdController> log)
    {
        _client = client;
        _registro = registro;
        _logs = logs;
        _log = log;
    }

    [HttpGet("atual")]
    public ActionResult<DfdAtualDto> ObterAtual()
    {
        var atual = _registro.Obter();
        var itens = _registro.ListarItens();
        var responsaveis = _registro.ListarResponsaveis();
        return Ok(new DfdAtualDto(atual, itens, responsaveis));
    }

    [HttpPost]
    public async Task<ActionResult<DfdCriadoDto>> Criar(CancellationToken ct)
    {
        try
        {
            var dfd = await _client.CriarDfdAsync(ct);
            _registro.Definir(dfd);
            _log.LogInformation("DFD criado e registrado. idArtefato={IdA} idFormalizacao={IdF}", dfd.IdArtefato, dfd.IdFormalizacaoDemanda);
            _logs.Registrar(NivelLog.Sucesso, "DFD", $"DFD #{dfd.Numero}/{dfd.Ano} criado", $"idArtefato={dfd.IdArtefato} idFormalizacao={dfd.IdFormalizacaoDemanda}");
            return Ok(dfd);
        }
        catch (TokenIndisponivelException ex)
        {
            _logs.Registrar(NivelLog.Erro, "DFD", "Tentativa de criar DFD sem token", ex.Message);
            return Problem(detail: ex.Message, statusCode: 401, title: "Token indisponivel");
        }
        catch (ComprasGovHttpException ex)
        {
            _logs.Registrar(NivelLog.Erro, "DFD", $"Falha ao criar DFD (HTTP {ex.StatusHttp})", ex.Message);
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp, title: "Falha no Compras", instance: ex.CorpoBruto);
        }
    }

    [HttpDelete("atual")]
    public ActionResult Limpar()
    {
        _registro.Limpar();
        return NoContent();
    }

    [HttpPut("atual/informacoes-gerais")]
    public async Task<ActionResult<InformacoesGeraisAtualizadasDto>> AtualizarInformacoesGerais(
        [FromBody] AtualizarInformacoesGeraisEntradaApi corpo, CancellationToken ct)
    {
        var atual = _registro.Obter();
        if (atual is null)
        {
            return BadRequest(new { erro = "Nenhum DFD atual. Crie um DFD antes de atualizar informações gerais." });
        }

        if (corpo is null) return BadRequest(new { erro = "Body obrigatório." });
        if (string.IsNullOrWhiteSpace(corpo.Objeto)) return BadRequest(new { erro = "Descrição (objeto) obrigatória." });
        if (corpo.NivelPrioridade == NivelPrioridade.ALTO && string.IsNullOrWhiteSpace(corpo.JustificativaPrioridade))
        {
            return BadRequest(new { erro = "Justificativa de prioridade obrigatória quando prioridade é ALTO." });
        }

        var input = new AtualizarInformacoesGeraisInput(
            DataConclusaoContratacao: corpo.DataConclusaoContratacao,
            Objeto: corpo.Objeto,
            NivelPrioridade: corpo.NivelPrioridade,
            JustificativaEmergencial: corpo.JustificativaEmergencial,
            JustificativaPrioridade: corpo.JustificativaPrioridade
        );

        try
        {
            var atualizado = await _client.AtualizarInformacoesGeraisAsync(
                atual.IdFormalizacaoDemanda, atual.IdArtefato, atual.Uasg, atual.Numero, atual.Ano, input, ct);

            _logs.Registrar(NivelLog.Sucesso, "DFD",
                $"Informações gerais do DFD #{atual.Numero}/{atual.Ano} atualizadas",
                $"prioridade={atualizado.NivelPrioridade} dataPrevista={atualizado.DataPrevista:yyyy-MM-dd} objeto=\"{Truncar(atualizado.Objeto, 60)}\"");

            return Ok(atualizado);
        }
        catch (TokenIndisponivelException ex)
        {
            return Problem(detail: ex.Message, statusCode: 401, title: "Token indisponivel");
        }
        catch (ComprasGovHttpException ex)
        {
            _logs.Registrar(NivelLog.Erro, "DFD", $"Falha ao atualizar informações gerais (HTTP {ex.StatusHttp})", ex.Message);
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp, title: "Falha no Compras", instance: ex.CorpoBruto);
        }
    }

    private static string Truncar(string? s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "...");

    [HttpPut("atual/justificativa")]
    public async Task<ActionResult<JustificativaAtualizadaDto>> AtualizarJustificativa(
        [FromBody] AtualizarJustificativaEntradaApi corpo, CancellationToken ct)
    {
        var atual = _registro.Obter();
        if (atual is null)
        {
            return BadRequest(new { erro = "Nenhum DFD atual. Crie um DFD antes de atualizar a justificativa." });
        }
        if (corpo is null) return BadRequest(new { erro = "Body obrigatório." });

        var idSecao = atual.Secoes.IdSecaoPrincipal;
        var idItem = atual.Secoes.IdItemJustificativaNecessidade ?? 0;
        if (idSecao == 0 || idItem == 0)
        {
            return BadRequest(new { erro = "Estrutura de seções do DFD não foi capturada na criação. Recrie o DFD." });
        }

        try
        {
            var atualizado = await _client.AtualizarJustificativaNecessidadeAsync(idSecao, idItem, corpo.Texto ?? string.Empty, ct);
            _logs.Registrar(NivelLog.Sucesso, "DFD",
                $"Justificativa do DFD #{atual.Numero}/{atual.Ano} atualizada",
                $"idItem={atualizado.Id} tamanho={(corpo.Texto ?? string.Empty).Length} chars");
            return Ok(atualizado);
        }
        catch (TokenIndisponivelException ex)
        {
            return Problem(detail: ex.Message, statusCode: 401, title: "Token indisponivel");
        }
        catch (ComprasGovHttpException ex)
        {
            _logs.Registrar(NivelLog.Erro, "DFD", $"Falha ao atualizar justificativa (HTTP {ex.StatusHttp})", ex.Message);
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp, title: "Falha no Compras", instance: ex.CorpoBruto);
        }
    }

    [HttpPost("atual/responsavel")]
    public async Task<ActionResult<ResponsavelCriadoDto>> AdicionarResponsavel(
        [FromBody] AdicionarResponsavelEntradaApi corpo, CancellationToken ct)
    {
        var atual = _registro.Obter();
        if (atual is null)
        {
            return BadRequest(new { erro = "Nenhum DFD atual. Crie um DFD antes de adicionar responsáveis." });
        }
        if (corpo is null) return BadRequest(new { erro = "Body obrigatório." });
        if (string.IsNullOrWhiteSpace(corpo.Cpf)) return BadRequest(new { erro = "CPF obrigatório." });
        if (string.IsNullOrWhiteSpace(corpo.Nome)) return BadRequest(new { erro = "Nome obrigatório." });
        if (string.IsNullOrWhiteSpace(corpo.Email)) return BadRequest(new { erro = "Email obrigatório." });
        if (string.IsNullOrWhiteSpace(corpo.Cargo)) return BadRequest(new { erro = "Cargo obrigatório." });

        var ordem = _registro.ListarResponsaveis().Count;
        var input = new AdicionarResponsavelInput(corpo.Cpf, corpo.Nome, corpo.Email, corpo.Cargo);

        try
        {
            var criado = await _client.AdicionarResponsavelAsync(
                atual.IdArtefato, atual.Numero, atual.Ano, ordem, input, ct);
            _registro.RegistrarResponsavel(criado);
            _logs.Registrar(NivelLog.Sucesso, "DFD",
                $"Responsável '{criado.Nome}' adicionado ao DFD #{atual.Numero}/{atual.Ano}",
                $"cpf={MascararCpf(criado.Cpf)} cargo={criado.Cargo}");
            return Ok(criado);
        }
        catch (TokenIndisponivelException ex)
        {
            return Problem(detail: ex.Message, statusCode: 401, title: "Token indisponivel");
        }
        catch (ComprasGovHttpException ex)
        {
            _logs.Registrar(NivelLog.Erro, "DFD", $"Falha ao adicionar responsável (HTTP {ex.StatusHttp})", ex.Message);
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp, title: "Falha no Compras", instance: ex.CorpoBruto);
        }
    }

    private static string MascararCpf(string cpf)
    {
        var d = new string(cpf.Where(char.IsDigit).ToArray());
        return d.Length == 11 ? $"{d[..3]}.***.***-{d[^2..]}" : cpf;
    }

    [HttpPost("atual/material")]
    public async Task<ActionResult<MaterialServicoCriadoDto>> AdicionarMaterial([FromBody] MaterialServicoEntradaApi corpo, CancellationToken ct)
    {
        var atual = _registro.Obter();
        if (atual is null)
        {
            return BadRequest(new { erro = "Nenhum DFD atual. Crie um DFD antes de adicionar material." });
        }

        var input = corpo.ParaInput(atual.IdFormalizacaoDemanda);

        try
        {
            var item = await _client.AdicionarMaterialServicoAsync(input, ct);
            _registro.RegistrarItem(item);
            _logs.Registrar(NivelLog.Sucesso, "Item", $"{item.Tipo} {item.Codigo} adicionado", $"idItem={item.Id} qtd={item.Quantidade} total=R$ {item.ValorTotal:F2}");
            return Ok(item);
        }
        catch (TokenIndisponivelException ex)
        {
            return Problem(detail: ex.Message, statusCode: 401, title: "Token indisponivel");
        }
        catch (ComprasGovHttpException ex)
        {
            _logs.Registrar(NivelLog.Erro, "Item", $"Falha ao adicionar item (HTTP {ex.StatusHttp})", ex.Message);
            return Problem(detail: ex.Message, statusCode: ex.StatusHttp, title: "Falha no Compras", instance: ex.CorpoBruto);
        }
    }
}

public sealed record DfdAtualDto(
    DfdCriadoDto? Atual,
    IReadOnlyList<MaterialServicoCriadoDto> Itens,
    IReadOnlyList<ResponsavelCriadoDto> Responsaveis
);

public sealed record AdicionarResponsavelEntradaApi(
    string Cpf,
    string Nome,
    string Email,
    string Cargo
);

public sealed record AtualizarJustificativaEntradaApi(string Texto);

public sealed record AtualizarInformacoesGeraisEntradaApi(
    DateOnly DataConclusaoContratacao,
    string Objeto,
    NivelPrioridade NivelPrioridade,
    string? JustificativaEmergencial,
    string? JustificativaPrioridade
);

public sealed record MaterialServicoEntradaApi(
    string Tipo,
    string Codigo,
    int IdClasse,
    string NomeClasse,
    int IdPadraoDescritivo,
    string NomePadraoDescritivo,
    string Descricao,
    decimal Quantidade,
    decimal ValorUnitario,
    string Moeda,
    string SiglaUnidadeFornecimento)
{
    public MaterialServicoInput ParaInput(long idFormalizacao) => new(
        IdFormalizacaoDemanda: idFormalizacao,
        Tipo: Tipo,
        Codigo: Codigo,
        IdClasse: IdClasse,
        NomeClasse: NomeClasse,
        IdPadraoDescritivo: IdPadraoDescritivo,
        NomePadraoDescritivo: NomePadraoDescritivo,
        Descricao: Descricao,
        Quantidade: Quantidade,
        ValorUnitario: ValorUnitario,
        Moeda: Moeda,
        SiglaUnidadeFornecimento: SiglaUnidadeFornecimento
    );
}
