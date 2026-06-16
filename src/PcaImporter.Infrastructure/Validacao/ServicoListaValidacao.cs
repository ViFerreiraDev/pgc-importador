using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PcaImporter.Application.Importacao;
using PcaImporter.Application.Logs;
using PcaImporter.Application.Validacao;
using PcaImporter.Infrastructure.Importacao;
using PcaImporter.Infrastructure.Persistencia;
using PcaImporter.Infrastructure.Persistencia.Entidades;

namespace PcaImporter.Infrastructure.Validacao;

public sealed class ServicoListaValidacao : IServicoListaValidacao
{
    /// <summary>Versão do algoritmo de fingerprint — incrementar invalida ticks antigos.</summary>
    private const int FingerprintVersaoAtual = 1;

    private static readonly Regex PadraoSheetsUrl = new(
        @"(?:https?|htps):\/\/docs\.google\.com\/spreadsheets\/d\/([a-zA-Z0-9_-]+)[^\s)]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PadraoGrupoNoTexto = new(
        @"(?i)\bGRUPO[\s.\-]*0*(\d{1,4})\b",
        RegexOptions.Compiled);

    private readonly IDbContextFactory<PcaDbContext> _factory;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IRepositorioPrecosReferencia _precosRef;
    private readonly IEventosListaValidacao _eventos;
    private readonly IRegistroLogs _logs;
    private readonly ILogger<ServicoListaValidacao> _log;

    public ServicoListaValidacao(
        IDbContextFactory<PcaDbContext> factory,
        IHttpClientFactory httpFactory,
        IRepositorioPrecosReferencia precosRef,
        IEventosListaValidacao eventos,
        IRegistroLogs logs,
        ILogger<ServicoListaValidacao> log)
    {
        _factory = factory;
        _httpFactory = httpFactory;
        _precosRef = precosRef;
        _eventos = eventos;
        _logs = logs;
        _log = log;
    }

    public async Task<ListaValidacaoDto> ObterListaAsync(CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var links = await ctx.ListaLinks
            .AsNoTracking()
            .Include(l => l.Itens).ThenInclude(i => i.Revisoes)
            .OrderBy(l => l.Classe).ThenBy(l => l.NumeroGrupo).ThenBy(l => l.Id)
            .ToListAsync(ct).ConfigureAwait(false);

        var ativos = links.Where(l => l.ExcluidoEm is null).Select(MapearLink).ToList();
        var lixeira = links.Where(l => l.ExcluidoEm is not null).Select(MapearLink).ToList();
        var gaps = CalcularGaps(links.Where(l => l.ExcluidoEm is null));
        return new ListaValidacaoDto(ativos, lixeira, gaps);
    }

    private static List<GapClasseDto> CalcularGaps(IEnumerable<ListaLinkEntity> ativos)
    {
        var resultado = new List<GapClasseDto>();
        var grupos = ativos
            .Where(l => !string.IsNullOrWhiteSpace(l.Classe) && l.NumeroGrupo is > 0)
            .GroupBy(l => l.Classe!.Trim().ToUpperInvariant());
        foreach (var g in grupos.OrderBy(x => x.Key))
        {
            var nums = g.Select(l => l.NumeroGrupo!.Value).Distinct().OrderBy(n => n).ToList();
            if (nums.Count == 0) continue;
            var max = nums[^1];
            var presentes = nums.ToHashSet();
            var faltantes = new List<int>();
            for (var i = 1; i <= max; i++)
            {
                if (!presentes.Contains(i)) faltantes.Add(i);
            }
            resultado.Add(new GapClasseDto(g.Key, max, faltantes));
        }
        return resultado;
    }

    public async Task<LinkValidacaoDto?> AdicionarLinkAsync(string url, string? rotulo, string? classe, int? numeroGrupo, string? loginAtor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        if (!GoogleSheetsHelper.TentarExtrairId(url, out var idPlanilha))
        {
            throw new InvalidOperationException("URL não parece um link válido do Google Sheets.");
        }

        var classeNorm = string.IsNullOrWhiteSpace(classe) ? null : classe.Trim().ToUpperInvariant();

        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var jaExiste = await ctx.ListaLinks
            .AnyAsync(l => l.IdPlanilha == idPlanilha && l.ExcluidoEm == null, ct)
            .ConfigureAwait(false);
        if (jaExiste) throw new LinkJaCadastradoException(idPlanilha);

        if (classeNorm is not null && numeroGrupo is > 0)
        {
            var grupoOcupado = await ctx.ListaLinks.AsNoTracking()
                .AnyAsync(l => l.ExcluidoEm == null && l.Classe == classeNorm && l.NumeroGrupo == numeroGrupo, ct)
                .ConfigureAwait(false);
            if (grupoOcupado)
            {
                throw new InvalidOperationException(
                    $"Já existe um link ativo na classe {classeNorm} com o número {numeroGrupo}.");
            }
        }

        var rotuloFinal = string.IsNullOrWhiteSpace(rotulo)
            ? (classeNorm is not null && numeroGrupo is > 0 ? $"Grupo {numeroGrupo}" : null)
            : rotulo.Trim();

        var entidade = new ListaLinkEntity
        {
            Rotulo = rotuloFinal,
            Url = url,
            IdPlanilha = idPlanilha,
            Classe = classeNorm,
            NumeroGrupo = numeroGrupo > 0 ? numeroGrupo : null,
            Estado = "pendente",
            CriadoEm = DateTimeOffset.UtcNow,
            CriadoPorLogin = loginAtor,
        };
        ctx.ListaLinks.Add(entidade);
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

        var dto = MapearLink(entidade);
        _logs.Registrar(NivelLog.Info, "ListaValidacao",
            $"Link adicionado: {dto.Classe ?? "(sem classe)"} · {dto.Rotulo ?? dto.IdPlanilha}",
            detalhes: dto.Url, usuarioLogin: loginAtor);
        await _eventos.LinkAdicionadoAsync(dto).ConfigureAwait(false);
        return dto;
    }

    public async Task<IReadOnlyList<LinkValidacaoDto>> AdicionarLinksDoTextoAsync(string textoColado, string? classe, bool detectarNumeroGrupo, string? loginAtor, CancellationToken ct = default)
    {
        var diff = await CompararLoteAsync(textoColado, classe, detectarNumeroGrupo, loginAtor, ct).ConfigureAwait(false);
        return diff.Adicionados;
    }

    public async Task<DiffLoteDto> CompararLoteAsync(string textoColado, string? classe, bool detectarNumeroGrupo, string? loginAtor, CancellationToken ct = default)
    {
        var adicionados = new List<LinkValidacaoDto>();
        var duplicados = new List<DuplicadoLoteDto>();
        if (string.IsNullOrWhiteSpace(textoColado))
        {
            return new DiffLoteDto(adicionados, duplicados, Array.Empty<AusenteLoteDto>());
        }

        var classeNorm = string.IsNullOrWhiteSpace(classe) ? null : classe.Trim().ToUpperInvariant();

        await using (var ctxLeitura = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false))
        {
            var ativos = await ctxLeitura.ListaLinks.AsNoTracking()
                .Where(l => l.ExcluidoEm == null)
                .ToListAsync(ct).ConfigureAwait(false);
            var ativosPorId = ativos.ToDictionary(l => l.IdPlanilha, StringComparer.OrdinalIgnoreCase);

            // Parse do texto: idPlanilha + url + rotulo + numeroGrupo (detectado via "Grupo X")
            var coladosOrdem = new List<(string IdPlanilha, string Url, string Rotulo, int? NumeroGrupo)>();
            var coladosSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var linha in textoColado.Split('\n'))
            {
                foreach (Match m in PadraoSheetsUrl.Matches(linha))
                {
                    var urlBruta = m.Value;
                    var idPlanilha = m.Groups[1].Value;
                    if (!coladosSet.Add(idPlanilha)) continue;
                    var url = urlBruta.StartsWith("htps://", StringComparison.OrdinalIgnoreCase)
                        ? "https://" + urlBruta[7..]
                        : urlBruta;
                    var antes = linha[..m.Index].Trim().TrimEnd(':', '-', '–', '—', '|').Trim();

                    int? numero = null;
                    if (detectarNumeroGrupo)
                    {
                        var mg = PadraoGrupoNoTexto.Match(antes);
                        if (mg.Success && int.TryParse(mg.Groups[1].Value, out var n)) numero = n;
                    }

                    var rotulo = numero is > 0
                        ? $"Grupo {numero}"
                        : (string.IsNullOrWhiteSpace(antes) ? $"Link {coladosOrdem.Count + 1}" : antes);

                    coladosOrdem.Add((idPlanilha, url, rotulo, numero));
                }
            }

            // Próximo número sequencial (caso o texto não tenha "Grupo X" e a classe tiver outros números)
            var maxAtualNaClasse = classeNorm is null
                ? 0
                : ativos.Where(l => l.Classe == classeNorm && l.NumeroGrupo > 0).Select(l => l.NumeroGrupo!.Value).DefaultIfEmpty(0).Max();

            var paraAdicionar = new List<(string Url, string Rotulo, int? NumeroGrupo)>();
            foreach (var (id, url, rotulo, numero) in coladosOrdem)
            {
                if (ativosPorId.TryGetValue(id, out var existente))
                {
                    duplicados.Add(new DuplicadoLoteDto(
                        LinkExistenteId: existente.Id,
                        IdPlanilha: id,
                        RotuloColado: rotulo,
                        RotuloExistente: existente.Rotulo,
                        Url: url));
                }
                else
                {
                    paraAdicionar.Add((url, rotulo, numero));
                }
            }

            // Ausentes — só faz sentido restringir à classe alvo (se informada)
            var ausentes = ativos
                .Where(l => !coladosSet.Contains(l.IdPlanilha))
                .Where(l => classeNorm is null || l.Classe == classeNorm)
                .Select(l => new AusenteLoteDto(
                    LinkId: l.Id,
                    IdPlanilha: l.IdPlanilha,
                    Rotulo: l.Rotulo,
                    Url: l.Url,
                    Estado: l.Estado))
                .ToList();

            // Inserts. Resolve conflito de NumeroGrupo: se já estiver ocupado nessa classe,
            // tenta o próximo livre.
            var ocupados = classeNorm is null
                ? new HashSet<int>()
                : ativos.Where(l => l.Classe == classeNorm && l.NumeroGrupo > 0).Select(l => l.NumeroGrupo!.Value).ToHashSet();

            foreach (var (url, rotulo, numero) in paraAdicionar)
            {
                int? numeroParaSalvar = null;
                if (classeNorm is not null)
                {
                    if (numero is > 0 && !ocupados.Contains(numero.Value))
                    {
                        numeroParaSalvar = numero;
                    }
                    else
                    {
                        // próximo livre acima do maior
                        var candidato = Math.Max(maxAtualNaClasse, numero ?? 0) + 1;
                        while (ocupados.Contains(candidato)) candidato++;
                        numeroParaSalvar = candidato;
                        maxAtualNaClasse = candidato;
                    }
                    ocupados.Add(numeroParaSalvar.Value);
                }

                try
                {
                    var rotuloFinal = numeroParaSalvar is > 0 ? $"Grupo {numeroParaSalvar}" : rotulo;
                    var dto = await AdicionarLinkAsync(url, rotuloFinal, classeNorm, numeroParaSalvar, loginAtor, ct).ConfigureAwait(false);
                    if (dto is not null) adicionados.Add(dto);
                }
                catch (LinkJaCadastradoException)
                {
                    var existente = await BuscarPorIdPlanilhaAsync(url, ct).ConfigureAwait(false);
                    if (existente is not null)
                    {
                        duplicados.Add(new DuplicadoLoteDto(
                            LinkExistenteId: existente.Id,
                            IdPlanilha: existente.IdPlanilha,
                            RotuloColado: rotulo,
                            RotuloExistente: existente.Rotulo,
                            Url: url));
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _log.LogWarning(ex, "Falha ao adicionar {Url} na classe {Classe}", url, classeNorm);
                }
            }

            if (adicionados.Count > 0 || duplicados.Count > 0)
            {
                _logs.Registrar(NivelLog.Info, "ListaValidacao",
                    $"Comparou lote {classeNorm ?? "(sem classe)"} ({coladosOrdem.Count} URLs): {adicionados.Count} adicionados, {duplicados.Count} duplicados, {ausentes.Count} ausentes.",
                    usuarioLogin: loginAtor);
            }

            return new DiffLoteDto(adicionados, duplicados, ausentes);
        }
    }

    private async Task<ListaLinkEntity?> BuscarPorIdPlanilhaAsync(string url, CancellationToken ct)
    {
        if (!GoogleSheetsHelper.TentarExtrairId(url, out var idPlanilha)) return null;
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await ctx.ListaLinks.AsNoTracking()
            .FirstOrDefaultAsync(l => l.IdPlanilha == idPlanilha && l.ExcluidoEm == null, ct).ConfigureAwait(false);
    }

    public async Task<LinkValidacaoDto?> ValidarLinkAsync(int linkId, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var link = await ctx.ListaLinks
            .Include(l => l.Itens).ThenInclude(i => i.Revisoes)
            .FirstOrDefaultAsync(l => l.Id == linkId, ct)
            .ConfigureAwait(false);
        if (link is null) throw new LinkNaoEncontradoException(linkId);
        if (link.ExcluidoEm is not null)
        {
            throw new InvalidOperationException("Link está na lixeira — restaure antes de revalidar.");
        }

        link.Estado = "validando";
        link.MensagemErro = null;
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        await _eventos.LinkAtualizadoAsync(MapearLink(link)).ConfigureAwait(false);

        try
        {
            var http = _httpFactory.CreateClient("google-sheets");
            await using var stream = await GoogleSheetsHelper.BaixarComoXlsxAsync(http, link.Url, ct).ConfigureAwait(false);

            var (entrada, errosLeitura) = LeitorPlanilhaDfd.Ler(stream);
            // Mesmo com erros de validação, se a leitura captou a descrição, preserva.
            if (entrada is not null && !string.IsNullOrWhiteSpace(entrada.Descricao))
            {
                link.Descricao = entrada.Descricao.Trim();
            }
            if (entrada is null || errosLeitura.Count > 0)
            {
                // Erros de leitura — não roda divergências.
                await SincronizarItensAsync(ctx, link, errosLeitura, Array.Empty<DivergenciaValidacao>(), ct).ConfigureAwait(false);
                link.Estado = errosLeitura.Count > 0 ? "invalido" : "erro";
                link.TotalMateriais = entrada?.Materiais.Count ?? 0;
                link.MensagemErro = errosLeitura.Count > 0 ? null : "Não foi possível ler a planilha.";
                link.ValidadoEm = DateTimeOffset.UtcNow;
            }
            else
            {
                var (errosVal, _, divergencias) = ValidadorImportacao.Validar(entrada, _precosRef);
                await SincronizarItensAsync(ctx, link, errosVal, divergencias, ct).ConfigureAwait(false);
                link.Estado = errosVal.Count == 0 ? "valido" : "invalido";
                link.TotalMateriais = entrada.Materiais.Count;
                link.MensagemErro = null;
                link.ValidadoEm = DateTimeOffset.UtcNow;
            }
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            link.Estado = "erro";
            link.MensagemErro = ex.Message;
            link.ValidadoEm = DateTimeOffset.UtcNow;
            // limpa itens antigos pra não confundir
            ctx.ListaItens.RemoveRange(link.Itens);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            _log.LogWarning(ex, "Falha ao validar link {LinkId}", linkId);
        }

        // recarrega itens + revisões pós-save
        await ctx.Entry(link).Collection(l => l.Itens).LoadAsync(ct).ConfigureAwait(false);
        foreach (var it in link.Itens)
        {
            await ctx.Entry(it).Collection(i => i.Revisoes).LoadAsync(ct).ConfigureAwait(false);
        }

        var dto = MapearLink(link);
        await _eventos.LinkAtualizadoAsync(dto).ConfigureAwait(false);
        return dto;
    }

    /// <summary>
    /// Diff entre itens atuais (banco) e itens calculados agora: upsert por fingerprint,
    /// delete dos que sumiram. Revisões dos que continuam são preservadas automaticamente
    /// pela igualdade de fingerprint.
    /// </summary>
    private async Task SincronizarItensAsync(
        PcaDbContext ctx,
        ListaLinkEntity link,
        IReadOnlyList<ErroValidacao> erros,
        IReadOnlyList<DivergenciaValidacao> divergencias,
        CancellationToken ct)
    {
        var novos = new Dictionary<string, ListaItemEntity>(StringComparer.Ordinal);

        foreach (var e in erros)
        {
            var fp = CalcularFingerprint(link.Id, "erro", e.Local, e.Campo, e.Linha, codigo: null);
            novos[fp] = new ListaItemEntity
            {
                LinkId = link.Id,
                Tipo = "erro",
                Fingerprint = fp,
                FingerprintVersao = FingerprintVersaoAtual,
                Local = e.Local,
                Campo = e.Campo,
                Linha = e.Linha,
                Codigo = null,
                DeltaPct = null,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    mensagem = e.Mensagem,
                    local = e.Local,
                    campo = e.Campo,
                    linha = e.Linha,
                }),
                CriadoEm = DateTimeOffset.UtcNow,
            };
        }

        foreach (var d in divergencias)
        {
            var fp = CalcularFingerprint(link.Id, "divergencia", d.Local, d.Tipo, d.Linha, codigo: d.Codigo);
            novos[fp] = new ListaItemEntity
            {
                LinkId = link.Id,
                Tipo = "divergencia",
                Fingerprint = fp,
                FingerprintVersao = FingerprintVersaoAtual,
                Local = d.Local,
                Campo = d.Tipo, // "preco" ou "qtd"
                Linha = d.Linha,
                Codigo = d.Codigo,
                DeltaPct = (double)d.DiferencaPct,
                PayloadJson = JsonSerializer.Serialize(new
                {
                    mensagem = d.Mensagem,
                    tipo = d.Tipo,
                    codigo = d.Codigo,
                    valorPlanilha = d.ValorPlanilha,
                    referenciaMin = d.ReferenciaMin,
                    referenciaMax = d.ReferenciaMax,
                    diferencaPct = d.DiferencaPct,
                    siglaReferencia = d.SiglaReferencia,
                    totalRegistros = d.TotalRegistros,
                    linha = d.Linha,
                }),
                CriadoEm = DateTimeOffset.UtcNow,
            };
        }

        var atuais = link.Itens.ToList();
        var atuaisPorFp = atuais.ToDictionary(i => i.Fingerprint, StringComparer.Ordinal);

        // Remove os que sumiram
        foreach (var atual in atuais)
        {
            if (!novos.ContainsKey(atual.Fingerprint))
            {
                ctx.ListaItens.Remove(atual);
            }
        }
        // Atualiza payload dos existentes (sem mexer em revisões)
        foreach (var (fp, novo) in novos)
        {
            if (atuaisPorFp.TryGetValue(fp, out var existente))
            {
                existente.Tipo = novo.Tipo;
                existente.Local = novo.Local;
                existente.Campo = novo.Campo;
                existente.Linha = novo.Linha;
                existente.Codigo = novo.Codigo;
                existente.DeltaPct = novo.DeltaPct;
                existente.PayloadJson = novo.PayloadJson;
                existente.FingerprintVersao = FingerprintVersaoAtual;
            }
            else
            {
                ctx.ListaItens.Add(novo);
            }
        }
        await Task.CompletedTask;
    }

    public async Task ExcluirLinkAsync(int linkId, string? loginAtor, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var link = await ctx.ListaLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct).ConfigureAwait(false);
        if (link is null) throw new LinkNaoEncontradoException(linkId);
        if (link.ExcluidoEm is not null) return; // já excluído

        link.ExcluidoEm = DateTimeOffset.UtcNow;
        link.ExcluidoPorLogin = loginAtor;
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        _logs.Registrar(NivelLog.Aviso, "ListaValidacao",
            $"Link excluído (lixeira): {link.Rotulo ?? link.IdPlanilha}",
            detalhes: link.Url, usuarioLogin: loginAtor);
        await _eventos.LinkExcluidoAsync(linkId, loginAtor).ConfigureAwait(false);
    }

    public async Task RestaurarLinkAsync(int linkId, string? loginAtor, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var link = await ctx.ListaLinks
            .Include(l => l.Itens).ThenInclude(i => i.Revisoes)
            .FirstOrDefaultAsync(l => l.Id == linkId, ct).ConfigureAwait(false);
        if (link is null) throw new LinkNaoEncontradoException(linkId);
        if (link.ExcluidoEm is null) return;

        // se já existe ativo com mesmo idPlanilha, falha
        var conflito = await ctx.ListaLinks.AnyAsync(
            l => l.IdPlanilha == link.IdPlanilha && l.ExcluidoEm == null && l.Id != linkId, ct).ConfigureAwait(false);
        if (conflito)
        {
            throw new InvalidOperationException(
                "Não dá pra restaurar: já existe outro link ativo com a mesma planilha. Exclua o ativo antes.");
        }

        link.ExcluidoEm = null;
        link.ExcluidoPorLogin = null;
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        _logs.Registrar(NivelLog.Info, "ListaValidacao",
            $"Link restaurado da lixeira: {link.Rotulo ?? link.IdPlanilha}",
            detalhes: link.Url, usuarioLogin: loginAtor);
        await _eventos.LinkRestauradoAsync(MapearLink(link)).ConfigureAwait(false);
    }

    public async Task ApagarDefinitivamenteAsync(int linkId, string? loginAtor, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var link = await ctx.ListaLinks.FirstOrDefaultAsync(l => l.Id == linkId, ct).ConfigureAwait(false);
        if (link is null) return;
        var rotulo = link.Rotulo ?? link.IdPlanilha;
        var url = link.Url;
        ctx.ListaLinks.Remove(link);
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        _logs.Registrar(NivelLog.Aviso, "ListaValidacao",
            $"Link apagado definitivamente: {rotulo}",
            detalhes: url, usuarioLogin: loginAtor);
        await _eventos.LinkApagadoDefinitivamenteAsync(linkId).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RevisorDto>> RevisarItemAsync(int itemId, string login, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var existe = await ctx.ListaItemRevisoes
            .AnyAsync(r => r.ItemId == itemId && r.RevisadoPorLogin == login, ct).ConfigureAwait(false);
        if (!existe)
        {
            ctx.ListaItemRevisoes.Add(new ListaItemRevisaoEntity
            {
                ItemId = itemId,
                RevisadoPorLogin = login,
                RevisadoEm = DateTimeOffset.UtcNow,
            });
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
            // Log só quando é nova revisão (pra não poluir).
            var item = await ctx.ListaItens.Include(i => i.Link)
                .FirstOrDefaultAsync(i => i.Id == itemId, ct).ConfigureAwait(false);
            if (item is not null)
            {
                _logs.Registrar(NivelLog.Info, "ListaValidacao",
                    $"Item revisado em '{item.Link.Rotulo ?? item.Link.IdPlanilha}'",
                    detalhes: $"{item.Tipo} · {item.Local ?? "?"} · linha {item.Linha?.ToString() ?? "?"} · {item.Codigo ?? item.Campo ?? ""}".Trim(),
                    usuarioLogin: login);
            }
        }
        var revisores = await CarregarRevisoresAsync(ctx, itemId, ct).ConfigureAwait(false);
        await _eventos.ItemRevisadoAsync(itemId, revisores).ConfigureAwait(false);
        return revisores;
    }

    public async Task<IReadOnlyList<RevisorDto>> DesrevisarItemAsync(int itemId, string login, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var revisao = await ctx.ListaItemRevisoes
            .FirstOrDefaultAsync(r => r.ItemId == itemId && r.RevisadoPorLogin == login, ct).ConfigureAwait(false);
        if (revisao is not null)
        {
            ctx.ListaItemRevisoes.Remove(revisao);
            await ctx.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        var revisores = await CarregarRevisoresAsync(ctx, itemId, ct).ConfigureAwait(false);
        await _eventos.ItemRevisadoAsync(itemId, revisores).ConfigureAwait(false);
        return revisores;
    }

    public async Task<LinkValidacaoDto?> RegistrarResultadoImportacaoAsync(
        int linkId, string idExecucao, bool sucesso, string? mensagemErro, CancellationToken ct = default)
    {
        await using var ctx = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var link = await ctx.ListaLinks
            .Include(l => l.Itens).ThenInclude(i => i.Revisoes)
            .FirstOrDefaultAsync(l => l.Id == linkId, ct).ConfigureAwait(false);
        if (link is null) throw new LinkNaoEncontradoException(linkId);

        link.UltimoIdExecucao = string.IsNullOrWhiteSpace(idExecucao) ? null : idExecucao;
        if (sucesso)
        {
            link.ImportadoEm = DateTimeOffset.UtcNow;
            link.UltimoErroImportacao = null;
            _logs.Registrar(NivelLog.Sucesso, "ListaValidacao",
                $"Importação concluída · {link.Rotulo ?? link.IdPlanilha}",
                detalhes: $"execucao={idExecucao}");
        }
        else
        {
            link.UltimoErroImportacao = string.IsNullOrWhiteSpace(mensagemErro) ? "Falha desconhecida." : mensagemErro;
            _logs.Registrar(NivelLog.Erro, "ListaValidacao",
                $"Falha ao importar · {link.Rotulo ?? link.IdPlanilha}",
                detalhes: $"execucao={idExecucao} · {link.UltimoErroImportacao}");
        }
        await ctx.SaveChangesAsync(ct).ConfigureAwait(false);

        var dto = MapearLink(link);
        await _eventos.LinkAtualizadoAsync(dto).ConfigureAwait(false);
        return dto;
    }

    private static async Task<IReadOnlyList<RevisorDto>> CarregarRevisoresAsync(PcaDbContext ctx, int itemId, CancellationToken ct)
    {
        var lista = await ctx.ListaItemRevisoes
            .AsNoTracking()
            .Where(r => r.ItemId == itemId)
            .OrderBy(r => r.RevisadoEm)
            .Select(r => new RevisorDto(r.RevisadoPorLogin, r.RevisadoEm))
            .ToListAsync(ct).ConfigureAwait(false);
        return lista;
    }

    private static LinkValidacaoDto MapearLink(ListaLinkEntity l)
    {
        var erros = l.Itens.Where(i => i.Tipo == "erro").Select(MapearItem).ToList();
        var divergencias = l.Itens.Where(i => i.Tipo == "divergencia").Select(MapearItem).ToList();
        return new LinkValidacaoDto(
            Id: l.Id,
            Rotulo: l.Rotulo,
            Url: l.Url,
            IdPlanilha: l.IdPlanilha,
            Classe: l.Classe,
            NumeroGrupo: l.NumeroGrupo,
            Estado: l.Estado,
            TotalMateriais: l.TotalMateriais,
            MensagemErro: l.MensagemErro,
            ValidadoEm: l.ValidadoEm,
            CriadoEm: l.CriadoEm,
            CriadoPorLogin: l.CriadoPorLogin,
            ExcluidoEm: l.ExcluidoEm,
            ExcluidoPorLogin: l.ExcluidoPorLogin,
            Descricao: l.Descricao,
            ImportadoEm: l.ImportadoEm,
            UltimoIdExecucao: l.UltimoIdExecucao,
            UltimoErroImportacao: l.UltimoErroImportacao,
            Erros: erros,
            Divergencias: divergencias,
            Avisos: Array.Empty<AvisoListaDto>()
        );
    }

    private static ItemValidacaoDto MapearItem(ListaItemEntity i)
    {
        // Tira "mensagem" do payload pra ficar acessível no DTO (front prefere campo direto).
        string mensagem = "";
        try
        {
            using var doc = JsonDocument.Parse(i.PayloadJson);
            if (doc.RootElement.TryGetProperty("mensagem", out var m)) mensagem = m.GetString() ?? "";
        }
        catch { /* ignora */ }

        var revisores = i.Revisoes
            .OrderBy(r => r.RevisadoEm)
            .Select(r => new RevisorDto(r.RevisadoPorLogin, r.RevisadoEm))
            .ToList();
        return new ItemValidacaoDto(
            Id: i.Id,
            Tipo: i.Tipo,
            Fingerprint: i.Fingerprint,
            Local: i.Local,
            Campo: i.Campo,
            Linha: i.Linha,
            Codigo: i.Codigo,
            DeltaPct: i.DeltaPct,
            Mensagem: mensagem,
            PayloadJson: i.PayloadJson,
            CriadoEm: i.CriadoEm,
            Revisores: revisores
        );
    }

    private static string CalcularFingerprint(int linkId, string tipo, string? local, string? campo, int? linha, string? codigo)
    {
        var raw = $"{linkId}|{tipo}|{local ?? ""}|{campo ?? ""}|{linha?.ToString() ?? ""}|{codigo ?? ""}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
