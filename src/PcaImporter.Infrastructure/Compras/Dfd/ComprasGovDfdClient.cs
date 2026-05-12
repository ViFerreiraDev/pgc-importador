using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Compras.Dfd;
using PcaImporter.Application.Token;

namespace PcaImporter.Infrastructure.Compras.Dfd;

public sealed class ComprasGovDfdClient : IComprasGovDfdClient
{
    private const string CaminhoCriarDfd = "/comprasnet-artefatos/api/v1/artefato/DFD";
    private const string CaminhoAdicionarMaterialServico = "/comprasnet-artefatos/api/v1/artefato/dfd/materialservico";
    private const string CaminhoAtualizarDfd = "/comprasnet-artefatos/api/v1/artefato/dfd";
    private const string CaminhoAtualizarItemSecao = "/comprasnet-artefatos/api/v1/artefato/secao/item";
    private const string CaminhoAdicionarResponsavel = "/comprasnet-artefatos/api/v1/artefato/responsavel";

    private static readonly JsonSerializerOptions OpcoesJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _http;
    private readonly IGerenciadorTokenSessao _gerenciador;
    private readonly ComprasGovOptions _opcoes;
    private readonly ILogger<ComprasGovDfdClient> _log;
    private readonly TimeProvider _tempo;

    public ComprasGovDfdClient(
        HttpClient http,
        IGerenciadorTokenSessao gerenciador,
        Microsoft.Extensions.Options.IOptions<ComprasGovOptions> opcoes,
        ILogger<ComprasGovDfdClient> log,
        TimeProvider tempo)
    {
        _http = http;
        _gerenciador = gerenciador;
        _opcoes = opcoes.Value;
        _log = log;
        _tempo = tempo;
    }

    public async Task<DfdCriadoDto> CriarDfdAsync(CancellationToken ct = default)
    {
        var corpo = await EnviarAsync(HttpMethod.Post, CaminhoCriarDfd, "{}", ct).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(corpo);
        var raiz = doc.RootElement;

        var idArtefato = LerLong(raiz, "id");
        var numero = LerInt(raiz, "numero");
        var ano = LerInt(raiz, "ano");
        var uasg = LerInt(raiz, "uasg");
        var nomeUasg = LerString(raiz, "nomeUasg") ?? string.Empty;
        var status = LerString(raiz, "status") ?? string.Empty;

        long idFormalizacao = 0;
        int? anoPca = null;
        string? statusPca = null;
        if (raiz.TryGetProperty("dfd", out var dfdEl) && dfdEl.ValueKind == JsonValueKind.Object)
        {
            idFormalizacao = LerLong(dfdEl, "id");
            if (dfdEl.TryGetProperty("pca", out var pcaEl) && pcaEl.ValueKind == JsonValueKind.Object)
            {
                anoPca = LerIntNullable(pcaEl, "ano");
                statusPca = LerString(pcaEl, "status");
            }
        }

        if (idFormalizacao == 0)
        {
            throw new ComprasGovHttpException(201, corpo, "Resposta de criacao de DFD nao trouxe dfd.id");
        }

        var secoes = ExtrairSecoes(raiz);

        return new DfdCriadoDto(
            IdArtefato: idArtefato,
            IdFormalizacaoDemanda: idFormalizacao,
            Numero: numero,
            Ano: ano,
            Uasg: uasg,
            NomeUasg: nomeUasg,
            Status: status,
            AnoPca: anoPca,
            StatusPca: statusPca,
            CriadoEm: _tempo.GetUtcNow(),
            Secoes: secoes,
            CorpoBruto: corpo
        );
    }

    private static SecoesDfd ExtrairSecoes(JsonElement raiz)
    {
        long idSecaoPrincipal = 0;
        long? infoGerais = null, justificativa = null, materiais = null, responsaveis = null, acompanhamento = null;

        if (raiz.TryGetProperty("secoes", out var secoesEl) && secoesEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var secao in secoesEl.EnumerateArray())
            {
                if (secao.ValueKind != JsonValueKind.Object) continue;
                var nome = LerString(secao, "nome") ?? string.Empty;
                var idSecao = LerLong(secao, "id");

                if (nome.StartsWith("Documento de Formaliza", StringComparison.OrdinalIgnoreCase))
                {
                    idSecaoPrincipal = idSecao;
                }

                if (!secao.TryGetProperty("itens", out var itens) || itens.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in itens.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var tipo = LerInt(item, "tipo");
                    var idItem = LerLong(item, "id");
                    switch (tipo)
                    {
                        case 12: infoGerais = idItem; break;
                        case 1:  justificativa = idItem; break;
                        case 13: materiais = idItem; break;
                        case 6:  responsaveis = idItem; break;
                        case 14: acompanhamento = idItem; break;
                    }
                }
            }
        }

        return new SecoesDfd(
            IdSecaoPrincipal: idSecaoPrincipal,
            IdItemInformacoesGerais: infoGerais,
            IdItemJustificativaNecessidade: justificativa,
            IdItemMateriaisServicos: materiais,
            IdItemResponsaveis: responsaveis,
            IdItemAcompanhamento: acompanhamento
        );
    }

    public async Task<MaterialServicoCriadoDto> AdicionarMaterialServicoAsync(MaterialServicoInput input, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(input, OpcoesJson);
        var corpo = await EnviarAsync(HttpMethod.Post, CaminhoAdicionarMaterialServico, json, ct).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(corpo);
        var raiz = doc.RootElement;

        return new MaterialServicoCriadoDto(
            Id: LerLong(raiz, "id"),
            IdFormalizacaoDemanda: LerLong(raiz, "idFormalizacaoDemanda"),
            Tipo: LerString(raiz, "tipo") ?? string.Empty,
            Codigo: LerString(raiz, "codigo") ?? string.Empty,
            IdClasse: LerInt(raiz, "idClasse"),
            NomeClasse: LerString(raiz, "nomeClasse") ?? string.Empty,
            IdPadraoDescritivo: LerInt(raiz, "idPadraoDescritivo"),
            NomePadraoDescritivo: LerString(raiz, "nomePadraoDescritivo") ?? string.Empty,
            Quantidade: LerDecimal(raiz, "quantidade"),
            ValorUnitario: LerDecimal(raiz, "valorUnitario"),
            ValorTotal: LerDecimal(raiz, "valorTotal"),
            Moeda: LerString(raiz, "moeda") ?? string.Empty,
            SiglaUnidadeFornecimento: LerString(raiz, "siglaUnidadeFornecimento") ?? string.Empty,
            NomeUnidadeFornecimento: LerString(raiz, "nomeUnidadeFornecimento") ?? string.Empty,
            DataHoraOperacao: LerDataHora(raiz, "dataHoraOperacao") ?? _tempo.GetUtcNow(),
            LoginOperacao: LerLong(raiz, "loginOperacao"),
            CorpoBruto: corpo
        );
    }

    public async Task<InformacoesGeraisAtualizadasDto> AtualizarInformacoesGeraisAsync(
        long idFormalizacaoDemanda,
        long idArtefato,
        int uasg,
        int numero,
        int ano,
        AtualizarInformacoesGeraisInput input,
        CancellationToken ct = default)
    {
        if (input.NivelPrioridade == NivelPrioridade.ALTO && string.IsNullOrWhiteSpace(input.JustificativaPrioridade))
        {
            throw new ArgumentException("Justificativa de prioridade obrigatoria quando prioridade e ALTO.", nameof(input));
        }

        // Referer especifico do DFD em edicao, identico ao que o front oficial envia.
        var referer = $"{_opcoes.BaseUrl}/comprasnet-artefatos-web/artefatos/edit/{idArtefato}?artefato={numero}%2F{ano}&tipo=DFD";

        // PASSO 1: read - PUT minimo para obter o estado atual completo do DFD.
        // O servidor aceita {id, idArtefato} e devolve o objeto inteiro com areaRequisitante,
        // pcaAreaList, valorTotalEstimado etc. Mais robusto que hardcoded local.
        var payloadLeitura = JsonSerializer.Serialize(new { id = idFormalizacaoDemanda, idArtefato });
        var corpoAtual = await EnviarAsync(HttpMethod.Put, CaminhoAtualizarDfd, payloadLeitura, ct, referer).ConfigureAwait(false);

        _log.LogInformation("DFD read antes do modify. Tamanho={Tam}", corpoAtual.Length);

        var raizAtual = JsonNode.Parse(corpoAtual);
        if (raizAtual is not JsonObject objAtual)
        {
            throw new ComprasGovHttpException(200, corpoAtual, "Resposta de leitura do DFD nao e um objeto JSON.");
        }

        // PASSO 2: modify - mexe somente nos campos editaveis pela nossa UI.
        var emergencial = input.NivelPrioridade == NivelPrioridade.ALTO;
        var dataIso = $"{input.DataConclusaoContratacao:yyyy-MM-dd}T00:00:00";

        objAtual["objeto"] = input.Objeto;
        objAtual["dataPrevista"] = dataIso;
        objAtual["nivelPrioridade"] = input.NivelPrioridade.ToString();
        objAtual["emergencial"] = emergencial;
        objAtual["justificativaEmergencial"] = null;
        objAtual["justificativaPrioridade"] = emergencial
            ? input.JustificativaPrioridade
            : (input.JustificativaPrioridade ?? string.Empty);

        // O servidor devolve o "molde" do DFD com varios campos null. Para o save persistir
        // corretamente, completamos os obrigatorios que faltam: uasgDestino e areaRequisitante.
        if (NodeEhVazioOuNulo(objAtual["uasgDestino"]))
        {
            objAtual["uasgDestino"] = uasg;
        }
        if (NodeEhVazioOuNulo(objAtual["areaRequisitante"]))
        {
            objAtual["areaRequisitante"] = MontarAreaRequisitanteJson(uasg);
        }
        else if (objAtual["areaRequisitante"] is JsonObject area)
        {
            // Garante que pcaAreaList esteja populado mesmo se o read trouxer o resto.
            if (area["pcaAreaList"] is null || (area["pcaAreaList"] is JsonArray arr && arr.Count == 0))
            {
                area["pcaAreaList"] = MontarPcaAreaListJson();
            }
        }


        // PASSO 3: write - PUT com o objeto completo modificado.
        var corpoFinal = await EnviarAsync(
            HttpMethod.Put,
            CaminhoAtualizarDfd,
            objAtual.ToJsonString(),
            ct,
            referer).ConfigureAwait(false);

        using var docFinal = JsonDocument.Parse(corpoFinal);
        var raizFinal = docFinal.RootElement;

        var nivel = LerString(raizFinal, "nivelPrioridade") switch
        {
            "ALTO" => NivelPrioridade.ALTO,
            "MEDIO" => NivelPrioridade.MEDIO,
            _ => NivelPrioridade.BAIXO,
        };

        return new InformacoesGeraisAtualizadasDto(
            Id: LerLong(raizFinal, "id"),
            IdArtefato: LerLong(raizFinal, "idArtefato"),
            Objeto: LerString(raizFinal, "objeto"),
            DataPrevista: LerDataHora(raizFinal, "dataPrevista"),
            NivelPrioridade: nivel,
            Emergencial: LerBool(raizFinal, "emergencial"),
            JustificativaEmergencial: LerString(raizFinal, "justificativaEmergencial"),
            ValorTotalEstimado: TentarLerDecimal(raizFinal, "valorTotalEstimado"),
            CorpoBruto: corpoFinal
        );
    }

    public async Task<JustificativaAtualizadaDto> AtualizarJustificativaNecessidadeAsync(
        long idSecao,
        long idItem,
        string textoSimples,
        CancellationToken ct = default)
    {
        if (idSecao == 0 || idItem == 0)
        {
            throw new ArgumentException("idSecao e idItem da Justificativa sao obrigatorios. Crie um DFD novo se o registro local nao tem esses ids.");
        }

        var token = await _gerenciador.ObterTokenValidoAsync(ct).ConfigureAwait(false);
        var loginOperacao = long.TryParse(token.Sub, out var l) ? l : 0L;

        var conteudoHtml = ConverterParaHtmlSimples(textoSimples);
        var dataHora = _tempo.GetUtcNow().ToOffset(TimeSpan.FromHours(-3))
            .ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");

        var payload = new Dictionary<string, object?>
        {
            ["id"] = idItem,
            ["idSecao"] = idSecao,
            ["tipo"] = 1,
            ["nomeItem"] = "Justificativa de Necessidade",
            ["ajuda"] = "Justificativa de Necessidade",
            ["detalhes"] = "Informe a justificativa para este DFD.",
            ["obrigatorio"] = "S",
            ["original"] = true,
            ["sigiloso"] = false,
            ["indice"] = null,
            ["ordem"] = 2,
            ["operacao"] = 2,
            ["loginOperacao"] = loginOperacao,
            ["dataHoraOperacao"] = dataHora,
            ["conteudo"] = conteudoHtml,
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        });

        var corpo = await EnviarAsync(HttpMethod.Put, CaminhoAtualizarItemSecao, json, ct).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(corpo);
        var raiz = doc.RootElement;

        return new JustificativaAtualizadaDto(
            Id: LerLong(raiz, "id"),
            IdSecao: LerLong(raiz, "idSecao"),
            Conteudo: LerString(raiz, "conteudo") ?? conteudoHtml,
            DataHoraOperacao: LerDataHora(raiz, "dataHoraOperacao") ?? _tempo.GetUtcNow(),
            CorpoBruto: corpo
        );
    }

    // Converte texto simples (multilinha) em HTML que o Compras espera no campo "conteudo".
    // Cada paragrafo (separado por linhas em branco) vira <p>...</p>; quebras simples viram <br/>.
    private static string ConverterParaHtmlSimples(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "<p></p>\n";

        var normalizado = texto.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (normalizado.Length == 0) return "<p></p>\n";

        var paragrafos = System.Text.RegularExpressions.Regex.Split(normalizado, "\n{2,}");
        var sb = new StringBuilder();
        foreach (var p in paragrafos)
        {
            var linhas = p.Split('\n');
            sb.Append("<p>");
            for (var i = 0; i < linhas.Length; i++)
            {
                sb.Append(EscaparHtml(linhas[i]));
                if (i < linhas.Length - 1) sb.Append("<br/>");
            }
            sb.Append("</p>");
        }
        sb.Append('\n');
        return sb.ToString();
    }

    private static string EscaparHtml(string s) => s
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");

    public async Task<ResponsavelCriadoDto> AdicionarResponsavelAsync(
        long idArtefato,
        int numero,
        int ano,
        int ordem,
        AdicionarResponsavelInput input,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Cpf)) throw new ArgumentException("CPF obrigatorio.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Nome)) throw new ArgumentException("Nome obrigatorio.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Email)) throw new ArgumentException("Email obrigatorio.", nameof(input));
        if (string.IsNullOrWhiteSpace(input.Cargo)) throw new ArgumentException("Cargo obrigatorio.", nameof(input));

        var p = _opcoes.ResponsavelPadrao;
        var cpfLimpo = new string(input.Cpf.Where(char.IsDigit).ToArray());
        var dataInstrumento = _tempo.GetUtcNow().ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var referer = $"{_opcoes.BaseUrl}/comprasnet-artefatos-web/artefatos/edit/{idArtefato}?artefato={numero}%2F{ano}&tipo=DFD";

        var payload = new Dictionary<string, object?>
        {
            ["nome"] = input.Nome.Trim(),
            ["cpf"] = cpfLimpo,
            ["idCargo"] = p.IdCargo,
            ["cargoDesc"] = input.Cargo.Trim(),
            ["despacho"] = string.Empty,
            ["operacao"] = 1,
            ["idArtefato"] = idArtefato,
            ["email"] = input.Email.Trim(),
            ["equipe"] = null,
            ["equipes"] = Array.Empty<object>(),
            ["instrumento"] = p.Instrumento,
            ["dataDoInstrumento"] = dataInstrumento,
            ["descricaoInstrumento"] = string.Empty,
            ["assinaDocumento"] = p.AssinaDocumento,
            ["ordem"] = ordem,
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        });

        var corpo = await EnviarAsync(HttpMethod.Post, CaminhoAdicionarResponsavel, json, ct, referer).ConfigureAwait(false);

        using var doc = JsonDocument.Parse(corpo);
        var raiz = doc.RootElement;

        return new ResponsavelCriadoDto(
            Id: raiz.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt64(out var id) ? id : null,
            IdArtefato: LerLong(raiz, "idArtefato"),
            Cpf: LerString(raiz, "cpf") ?? cpfLimpo,
            Nome: LerString(raiz, "nome") ?? input.Nome,
            Email: LerString(raiz, "email") ?? input.Email,
            IdCargo: LerInt(raiz, "idCargo"),
            Cargo: LerString(raiz, "cargoDesc") ?? input.Cargo,
            Instrumento: LerString(raiz, "instrumento") ?? p.Instrumento,
            AssinaDocumento: LerBool(raiz, "assinaDocumento"),
            Ordem: LerInt(raiz, "ordem"),
            CorpoBruto: corpo
        );
    }

    private const string UserAgentBrowser =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/147.0.0.0 Safari/537.36";

    private async Task<string> EnviarAsync(HttpMethod metodo, string caminho, string corpoJson, CancellationToken ct, string? refererEspecifico = null)
    {
        var token = await _gerenciador.ObterTokenValidoAsync(ct).ConfigureAwait(false);

        using var req = new HttpRequestMessage(metodo, caminho);

        // Headers identicos ao front oficial. Importante:
        // - "authorization" minusculo (evita filtros case-sensitive em backends Java legados)
        // - "content-type: application/json" SEM charset (backend Spring rejeita charset suffix)
        // - "accept-encoding: gzip" liga compressao (--compressed do curl)
        req.Headers.TryAddWithoutValidation("authorization", $"Bearer {token.AccessToken}");
        req.Headers.TryAddWithoutValidation("pragma", "no-cache");
        req.Headers.TryAddWithoutValidation("cache-control", "no-cache, no-store, max-age=0, must-revalidate");
        req.Headers.TryAddWithoutValidation("expires", "0");
        req.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        req.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Google Chrome\";v=\"147\", \"Not.A/Brand\";v=\"8\", \"Chromium\";v=\"147\"");
        req.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        req.Headers.TryAddWithoutValidation("ignorespinner", "true");
        req.Headers.TryAddWithoutValidation("user-agent", UserAgentBrowser);
        req.Headers.TryAddWithoutValidation("accept", "application/json, text/plain, */*");
        req.Headers.TryAddWithoutValidation("accept-encoding", "gzip, deflate, br");
        req.Headers.TryAddWithoutValidation("accept-language", "pt-BR,pt;q=0.9,en-US;q=0.8,en;q=0.7");
        req.Headers.TryAddWithoutValidation("origin", _opcoes.BaseUrl);
        req.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
        req.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        req.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        req.Headers.TryAddWithoutValidation("referer", refererEspecifico ?? $"{_opcoes.BaseUrl}/comprasnet-artefatos-web/");
        req.Headers.TryAddWithoutValidation("priority", "u=1, i");

        var bytesCorpo = Encoding.UTF8.GetBytes(corpoJson);
        var content = new ByteArrayContent(bytesCorpo);
        content.Headers.TryAddWithoutValidation("content-type", "application/json");
        req.Content = content;

        _log.LogInformation("ComprasGov {Metodo} {Caminho}", metodo, caminho);

        // Suprime o traceparent que .NET adiciona automaticamente: o servidor do Compras
        // nao envia esse header e nossa request com ele estava aceita mas marcada de forma
        // que o DFD desaparecia da listagem.
        var atividadeAtual = Activity.Current;
        Activity.Current = null;
        HttpResponseMessage resp;
        string corpo;
        try
        {
            resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            corpo = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            Activity.Current = atividadeAtual;
        }

        try
        {
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("ComprasGov {Metodo} {Caminho} retornou {Status}. Corpo: {Corpo}", metodo, caminho, (int)resp.StatusCode, Truncar(corpo));
                throw new ComprasGovHttpException((int)resp.StatusCode, corpo, $"HTTP {(int)resp.StatusCode} em {caminho}");
            }

            return corpo;
        }
        finally
        {
            resp.Dispose();
        }
    }

    private static long LerLong(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var v) ? v : 0L;

    private static int LerInt(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v) ? v : 0;

    private static int? LerIntNullable(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v) ? v : null;

    private static decimal LerDecimal(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v) ? v : 0m;

    private static decimal? TentarLerDecimal(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var v) ? v : null;

    private static bool LerBool(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && (p.ValueKind == JsonValueKind.True || p.ValueKind == JsonValueKind.False) && p.GetBoolean();

    private static bool NodeEhVazioOuNulo(JsonNode? n)
    {
        if (n is null) return true;
        if (n.GetValueKind() == JsonValueKind.Null) return true;
        if (n is JsonObject obj && obj.Count == 0) return true;
        return false;
    }

    private JsonObject MontarAreaRequisitanteJson(int uasg)
    {
        var p = _opcoes.AreaRequisitantePadrao;
        return new JsonObject
        {
            ["id"] = p.IdArea,
            ["uasg"] = uasg,
            ["nomeArea"] = p.NomeArea,
            ["areaRequisitanteUasg"] = new JsonObject
            {
                ["areaRequisitanteUasgId"] = new JsonObject
                {
                    ["idArea"] = p.IdArea,
                    ["uasg"] = uasg,
                },
                ["permissao"] = p.Permissao,
            },
            ["possuoPermissaoAdmin"] = p.PossuoPermissaoAdmin,
            ["pcaAreaList"] = MontarPcaAreaListJson(),
        };
    }

    // Lista de PCAs disponiveis para a area requisitante. Hardcoded com os 3 PCAs vigentes
    // da SMS-RJ (UASG 986001). TODO: substituir por endpoint que devolva a lista real.
    private static JsonArray MontarPcaAreaListJson() => new()
    {
        MontarPcaArea(680784, 2027, "EM_ELABORACAO", "2026-01-01T00:00:01.000-03:00", 1, 0m),
        MontarPcaArea(545106, 2026, "EM_EXECUCAO",   "2026-01-02T14:54:45.000-03:00", 2, 0m),
        MontarPcaArea(275509, 2025, "EXECUTADO",     "2026-01-02T14:54:45.000-03:00", 2, null),
    };

    private static JsonObject MontarPcaArea(int id, int ano, string status, string dataHoraOperacao, int operacao, decimal? orcamento)
    {
        var obj = new JsonObject
        {
            ["id"] = id,
            ["pca"] = new JsonObject
            {
                ["ano"] = ano,
                ["nome"] = $"PCA relativo ao ano de {ano}.",
                ["status"] = status,
                ["loginOperacao"] = 11111111111L,
                ["dataHoraOperacao"] = dataHoraOperacao,
                ["operacao"] = operacao,
                ["dataAnaliseIni"] = null,
                ["dataElaboracaoIni"] = null,
                ["dataElaboracaoFim"] = null,
                ["dataPrimeiroAjusteIni"] = null,
                ["dataPrimeiroAjusteFim"] = null,
                ["dataSegundoAjusteIni"] = null,
                ["dataSegundoAjusteFim"] = null,
            },
            ["orcamento"] = orcamento,
            ["saldoParcial"] = 0,
            ["totalDebitos"] = 0,
            ["contagemDfdsRelacionadas"] = 0,
        };
        return obj;
    }

    private static string? LerString(JsonElement el, string nome) =>
        el.TryGetProperty(nome, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateTimeOffset? LerDataHora(JsonElement el, string nome)
    {
        var s = LerString(el, nome);
        return DateTimeOffset.TryParse(s, out var dt) ? dt : null;
    }

    private static string Truncar(string s, int max = 500) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "...[truncado]");
}
