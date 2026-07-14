using Microsoft.Extensions.Logging;
using PcaImporter.Application.Catalogo;
using PcaImporter.Application.Compras.Catalogo;
using PcaImporter.Application.Compras.Dfd;
using PcaImporter.Application.Importacao;
using PcaImporter.Application.Logs;
using PcaImporter.Application.Token;
using PcaImporter.Domain.Token;
using System.Net.Http;

namespace PcaImporter.Infrastructure.Importacao;

public sealed class ServicoImportacao : IServicoImportacao
{
    private readonly IComprasGovDfdClient _dfd;
    private readonly IComprasGovCatalogoClient _catalogo;
    private readonly IRepositorioCatalogoMaterial _catalogoLocal;
    private readonly IRegistroDfdAtual _registro;
    private readonly RegistroImportacoes _registroImp;
    private readonly IRegistroLogs _logs;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IRepositorioHistoricoImportacao _historico;
    private readonly IRepositorioPrecosReferencia _precosRef;
    private readonly IGerenciadorTokenSessao _tokens;
    private readonly ILogger<ServicoImportacao> _log;

    // Mapa idExecucao -> contexto de origem (link + usuario), pra registrar histórico ao concluir.
    private readonly Dictionary<string, (string IdPlanilha, string Url, string? UsuarioLogin)> _origemDoLink = new();
    private readonly Lock _travaOrigem = new();

    public ServicoImportacao(
        IComprasGovDfdClient dfd,
        IComprasGovCatalogoClient catalogo,
        IRepositorioCatalogoMaterial catalogoLocal,
        IRegistroDfdAtual registro,
        RegistroImportacoes registroImp,
        IRegistroLogs logs,
        IHttpClientFactory httpFactory,
        IRepositorioHistoricoImportacao historico,
        IRepositorioPrecosReferencia precosRef,
        IGerenciadorTokenSessao tokens,
        ILogger<ServicoImportacao> log)
    {
        _dfd = dfd;
        _catalogo = catalogo;
        _catalogoLocal = catalogoLocal;
        _registro = registro;
        _registroImp = registroImp;
        _logs = logs;
        _httpFactory = httpFactory;
        _historico = historico;
        _precosRef = precosRef;
        _tokens = tokens;
        _log = log;
    }

    /// <summary>
    /// Garante que há sessão utilizável no Compras.gov antes de disparar uma importação.
    /// Validação de planilha NÃO passa por aqui — continua livre sem sessão.
    /// Se o token expirou mas há refresh, tenta recuperar antes de recusar.
    /// </summary>
    private async Task GarantirSessaoAsync(CancellationToken ct)
    {
        const string msgSemSessao =
            "O sistema não está logado no Compras.gov. Informe um novo token na tela de Sessão para importar.";

        var st = _tokens.ObterStatus();
        if (st.Estado == EstadoToken.Ausente)
        {
            throw new TokenIndisponivelException(msgSemSessao);
        }

        if ((st.SegundosRestantes ?? 0) <= 0)
        {
            st = await _tokens.ForcarRefreshAsync(ct).ConfigureAwait(false);
            if (st.Estado == EstadoToken.Ausente || (st.SegundosRestantes ?? 0) <= 0)
            {
                throw new TokenIndisponivelException(msgSemSessao);
            }
        }
    }

    public StatusImportacao? ObterStatus(string id) => _registroImp.Obter(id);

    public IReadOnlyList<StatusImportacao> ListarRecentes(int limite = 20) => _registroImp.ListarRecentes(limite);

    public Task<MetricasImportacaoDto> ObterMetricasAsync(CancellationToken ct = default) =>
        _historico.ObterMetricasAsync(ct);

    public Task<int> ResetarMetricasAsync(CancellationToken ct = default) =>
        _historico.LimparAsync(ct);

    public Task<ResultadoValidacaoImportacao> ValidarAsync(Stream xlsx, CancellationToken ct = default)
    {
        var (entrada, errosLeitura) = LeitorPlanilhaDfd.Ler(xlsx);
        if (entrada is null || errosLeitura.Count > 0)
        {
            return Task.FromResult(new ResultadoValidacaoImportacao(
                Valido: false,
                TotalMateriais: entrada?.Materiais.Count ?? 0,
                Erros: errosLeitura,
                Avisos: Array.Empty<AvisoValidacao>(),
                Divergencias: Array.Empty<DivergenciaValidacao>(),
                Entrada: entrada
            ));
        }

        var (errosVal, avisos, divergencias) = ValidadorImportacao.Validar(entrada, _precosRef);
        return Task.FromResult(new ResultadoValidacaoImportacao(
            Valido: errosVal.Count == 0,
            TotalMateriais: entrada.Materiais.Count,
            Erros: errosVal,
            Avisos: avisos,
            Divergencias: divergencias,
            Entrada: entrada
        ));
    }

    public async Task<ResultadoValidacaoImportacao> ValidarLinkAsync(string url, CancellationToken ct = default)
    {
        var http = _httpFactory.CreateClient("google-sheets");
        await using var stream = await GoogleSheetsHelper.BaixarComoXlsxAsync(http, url, ct).ConfigureAwait(false);
        return await ValidarAsync(stream, ct).ConfigureAwait(false);
    }

    public async Task<HistoricoImportacaoDto?> ConsultarHistoricoPorLinkAsync(string url, CancellationToken ct = default)
    {
        if (!GoogleSheetsHelper.TentarExtrairId(url, out var idPlanilha))
        {
            return null;
        }
        return await _historico.BuscarPorIdPlanilhaAsync(idPlanilha, ct).ConfigureAwait(false);
    }

    public async Task<string> IniciarLinkAsync(string url, bool confirmarDuplicado, string? usuarioLogin, CancellationToken ct = default)
    {
        await GarantirSessaoAsync(ct).ConfigureAwait(false);

        if (!GoogleSheetsHelper.TentarExtrairId(url, out var idPlanilha))
        {
            throw new InvalidOperationException(
                "URL do Google Sheets inválida. Esperado algo como https://docs.google.com/spreadsheets/d/<ID>/...");
        }

        if (!confirmarDuplicado)
        {
            var anterior = await _historico.BuscarPorIdPlanilhaAsync(idPlanilha, ct).ConfigureAwait(false);
            if (anterior is not null)
            {
                throw new ImportacaoDuplicadaException(anterior,
                    $"Esta planilha já foi importada em {anterior.ImportadaEm.ToLocalTime():dd/MM/yyyy HH:mm} " +
                    $"como DFD #{anterior.NumeroDfd}/{anterior.AnoDfd}.");
            }
        }

        var http = _httpFactory.CreateClient("google-sheets");
        await using var stream = await GoogleSheetsHelper.BaixarComoXlsxAsync(http, url, ct).ConfigureAwait(false);
        var idExecucao = await IniciarAsync(stream, ct).ConfigureAwait(false);

        // Marca origem para que o executor saiba registrar no historico ao concluir.
        lock (_travaOrigem)
        {
            _origemDoLink[idExecucao] = (idPlanilha, url, usuarioLogin);
        }

        return idExecucao;
    }

    public async Task<string> IniciarAsync(Stream xlsx, CancellationToken ct = default)
    {
        await GarantirSessaoAsync(ct).ConfigureAwait(false);

        var resultado = await ValidarAsync(xlsx, ct).ConfigureAwait(false);
        if (!resultado.Valido || resultado.Entrada is null)
        {
            throw new InvalidOperationException("Arquivo inválido. Use o endpoint de validar antes de iniciar.");
        }

        var entrada = resultado.Entrada;
        var estado = _registroImp.CriarNova();

        // Etapas: criar DFD + info gerais + justificativa + N materiais + responsavel = 4 + N
        _registroImp.Atualizar(estado.Id, e =>
        {
            e.TotalMateriais = entrada.Materiais.Count;
            e.TotalEtapas = 4 + entrada.Materiais.Count;
            e.Estado = EstadoImportacao.Executando;
            e.EtapaAtual = "Iniciando";
            e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Inicio", $"Importação iniciada com {entrada.Materiais.Count} materiais."));
        });

        _ = Task.Run(() => ExecutarAsync(estado.Id, entrada, CancellationToken.None));
        return estado.Id;
    }

    private async Task ExecutarAsync(string idImp, EntradaImportacaoDfd entrada, CancellationToken ct)
    {
        // Linha exata da planilha onde a falha ocorreu (preenchida no loop de materiais).
        int? linhaErroAtual = null;
        try
        {
            // ETAPA 1: criar DFD
            ProgressoEtapa(idImp, "Criando DFD");
            var dfdCriado = await _dfd.CriarDfdAsync(ct).ConfigureAwait(false);
            _registro.Definir(dfdCriado);
            _registroImp.Atualizar(idImp, e =>
            {
                e.IdArtefatoCriado = dfdCriado.IdArtefato;
                e.IdFormalizacaoCriado = dfdCriado.IdFormalizacaoDemanda;
                e.NumeroDfd = dfdCriado.Numero;
                e.AnoDfd = dfdCriado.Ano;
                e.EtapasConcluidas++;
                e.Eventos.Add(new EventoImportacao(
                    DateTimeOffset.UtcNow, "Sucesso",
                    $"DFD #{dfdCriado.Numero}/{dfdCriado.Ano} criado",
                    $"idArtefato={dfdCriado.IdArtefato} idFormalizacao={dfdCriado.IdFormalizacaoDemanda}"));
            });
            _logs.Registrar(NivelLog.Sucesso, "Importacao", $"[{idImp}] DFD criado");

            // ETAPA 2: informacoes gerais
            ProgressoEtapa(idImp, "Salvando Informações Gerais");
            var inputInfo = new AtualizarInformacoesGeraisInput(
                DataConclusaoContratacao: entrada.DataConclusaoContratacao!.Value,
                Objeto: entrada.Descricao!,
                NivelPrioridade: entrada.NivelPrioridade,
                JustificativaEmergencial: null,
                JustificativaPrioridade: entrada.NivelPrioridade == NivelPrioridade.ALTO ? entrada.JustificativaPrioridade : ""
            );
            await _dfd.AtualizarInformacoesGeraisAsync(
                dfdCriado.IdFormalizacaoDemanda, dfdCriado.IdArtefato,
                dfdCriado.Uasg, dfdCriado.Numero, dfdCriado.Ano,
                inputInfo, ct).ConfigureAwait(false);
            _registroImp.Atualizar(idImp, e =>
            {
                e.EtapasConcluidas++;
                e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Sucesso", "Informações gerais salvas"));
            });

            // ETAPA 3: justificativa de necessidade
            ProgressoEtapa(idImp, "Salvando Justificativa de Necessidade");
            await _dfd.AtualizarJustificativaNecessidadeAsync(
                dfdCriado.Secoes.IdSecaoPrincipal,
                dfdCriado.Secoes.IdItemJustificativaNecessidade ?? 0,
                entrada.JustificativaNecessidade!, ct).ConfigureAwait(false);
            _registroImp.Atualizar(idImp, e =>
            {
                e.EtapasConcluidas++;
                e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Sucesso", "Justificativa salva"));
            });

            // ETAPA 4: responsável (antes dos materiais — DFD passa a ter responsável atribuído primeiro)
            ProgressoEtapa(idImp, "Adicionando responsável");
            var ordem = _registro.ListarResponsaveis().Count;
            var inputResp = new AdicionarResponsavelInput(
                Cpf: new string((entrada.ResponsavelCpf ?? "").Where(char.IsDigit).ToArray()),
                Nome: entrada.ResponsavelNome!,
                Email: entrada.ResponsavelEmail!,
                Cargo: entrada.ResponsavelCargo!
            );
            var resp = await _dfd.AdicionarResponsavelAsync(
                dfdCriado.IdArtefato, dfdCriado.Numero, dfdCriado.Ano, ordem, inputResp, ct).ConfigureAwait(false);
            _registro.RegistrarResponsavel(resp);
            _registroImp.Atualizar(idImp, e =>
            {
                e.EtapasConcluidas++;
                e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Sucesso",
                    $"Responsável '{resp.Nome}' adicionado"));
            });

            // ETAPA 5..N+4: materiais (busca catalogo + adiciona ao DFD) — sempre por último.
            for (var i = 0; i < entrada.Materiais.Count; i++)
            {
                var m = entrada.Materiais[i];
                linhaErroAtual = m.LinhaPlanilha; // marca linha sob processamento
                var rotulo = $"Material {i + 1}/{entrada.Materiais.Count} (código {m.Codigo}, linha {m.LinhaPlanilha})";
                ProgressoEtapa(idImp, rotulo);

                ItemCatalogoDto? cat = null;
                var ehMaterial = (m.Tipo ?? "MATERIAL").Equals("MATERIAL", StringComparison.OrdinalIgnoreCase);
                if (ehMaterial && int.TryParse(m.Codigo, out var codigoInt))
                {
                    var local = await _catalogoLocal.BuscarPorCodigoAsync(codigoInt, ct).ConfigureAwait(false);
                    if (local is not null)
                    {
                        cat = new ItemCatalogoDto(
                            CodigoItem: local.CodigoItem,
                            CodigoGrupo: local.CodigoGrupo,
                            NomeGrupo: local.NomeGrupo,
                            CodigoClasse: local.CodigoClasse,
                            NomeClasse: local.NomeClasse,
                            CodigoPdm: local.CodigoPdm,
                            NomePdm: local.NomePdm,
                            DescricaoItem: local.DescricaoItem,
                            StatusItem: local.StatusItem,
                            ItemSustentavel: local.ItemSustentavel,
                            // mesmo valor que ComprasGovCatalogoClient.ConsultarMaterialAsync usa
                            Tipo: "MATERIAL",
                            DataHoraAtualizacao: local.AtualizadoEmFonte,
                            CorpoBruto: string.Empty);
                    }
                }

                if (cat is null)
                {
                    try
                    {
                        cat = await Resiliencia.TentarComBackoffAsync(
                            async (token) => ehMaterial
                                ? await _catalogo.ConsultarMaterialAsync(m.Codigo!, token).ConfigureAwait(false)
                                : await _catalogo.ConsultarServicoAsync(m.Codigo!, token).ConfigureAwait(false),
                            aoFalhar: (tent, ex, espera) =>
                            {
                                _registroImp.Atualizar(idImp, e =>
                                {
                                    e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Aviso",
                                        $"Tentativa {tent} de consultar catálogo {m.Codigo} falhou. Aguardando {espera.TotalSeconds:F0}s antes de tentar novamente.",
                                        ex.Message));
                                });
                            },
                            ct: ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _registroImp.Atualizar(idImp, e =>
                        {
                            e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Erro",
                                $"Falha definitiva ao consultar catálogo {m.Codigo} após retentativas", ex.Message));
                        });
                        throw;
                    }
                }

                if (cat is null)
                {
                    var err = $"Item {m.Codigo} não encontrado no catálogo.";
                    _registroImp.Atualizar(idImp, e =>
                    {
                        e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Erro", err));
                    });
                    throw new InvalidOperationException(err);
                }

                var inputMat = new MaterialServicoInput(
                    IdFormalizacaoDemanda: dfdCriado.IdFormalizacaoDemanda,
                    Tipo: cat.Tipo,
                    Codigo: cat.CodigoItem.ToString(),
                    IdClasse: cat.CodigoClasse,
                    NomeClasse: cat.NomeClasse,
                    IdPadraoDescritivo: cat.CodigoPdm > 0 ? cat.CodigoPdm : null,
                    NomePadraoDescritivo: string.IsNullOrWhiteSpace(cat.NomePdm) ? null : cat.NomePdm,
                    Descricao: cat.DescricaoItem,
                    Quantidade: m.Quantidade!.Value,
                    ValorUnitario: m.ValorUnitario!.Value,
                    Moeda: m.Moeda ?? "Real",
                    SiglaUnidadeFornecimento: m.SiglaUnidadeFornecimento!
                );
                MaterialServicoCriadoDto criadoMat;
                try
                {
                    criadoMat = await Resiliencia.TentarComBackoffAsync(
                        (token) => _dfd.AdicionarMaterialServicoAsync(inputMat, token),
                        aoFalhar: (tent, ex, espera) =>
                        {
                            _registroImp.Atualizar(idImp, e =>
                            {
                                e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Aviso",
                                    $"Tentativa {tent} de enviar material {m.Codigo} ao DFD falhou. Aguardando {espera.TotalSeconds:F0}s antes de tentar novamente.",
                                    ex.Message));
                            });
                        },
                        ct: ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _registroImp.Atualizar(idImp, e =>
                    {
                        e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Erro",
                            $"Falha definitiva ao enviar material {m.Codigo} após retentativas", ex.Message));
                    });
                    throw;
                }
                _registro.RegistrarItem(criadoMat);
                _registroImp.Atualizar(idImp, e =>
                {
                    e.MateriaisAdicionados++;
                    e.EtapasConcluidas++;
                    e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Sucesso",
                        $"{rotulo} adicionado",
                        $"idItem={criadoMat.Id} total=R$ {criadoMat.ValorTotal:F2}"));
                });
                linhaErroAtual = null; // material processado com sucesso, nada para "culpar"
            }

            // FINALIZADO
            _registroImp.Atualizar(idImp, e =>
            {
                e.Estado = EstadoImportacao.Concluida;
                e.ConcluidaEm = DateTimeOffset.UtcNow;
                e.EtapaAtual = null;
                e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Conclusao",
                    $"Importação concluída. DFD #{e.NumeroDfd}/{e.AnoDfd} pronto."));
            });
            _logs.Registrar(NivelLog.Sucesso, "Importacao",
                $"[{idImp}] Importação concluída: DFD #{entrada.Materiais.Count} materiais");

            // Se veio de link, registra no historico para deteccao futura de duplicado.
            (string IdPlanilha, string Url, string? UsuarioLogin) origem;
            bool teveOrigem;
            lock (_travaOrigem)
            {
                teveOrigem = _origemDoLink.Remove(idImp, out origem);
            }
            if (teveOrigem)
            {
                try
                {
                    var snapshot = _registroImp.Obter(idImp)!;
                    var valorTotalImp = entrada.Materiais.Sum(m =>
                        (m.Quantidade ?? 0m) * (m.ValorUnitario ?? 0m));
                    await _historico.RegistrarAsync(new HistoricoImportacaoDto(
                        Id: 0,
                        IdPlanilha: origem.IdPlanilha,
                        UrlOriginal: origem.Url,
                        ImportadaEm: DateTimeOffset.UtcNow,
                        IdExecucao: idImp,
                        NumeroDfd: snapshot.NumeroDfd ?? 0,
                        AnoDfd: snapshot.AnoDfd ?? 0,
                        IdArtefato: snapshot.IdArtefatoCriado ?? 0,
                        IdFormalizacaoDemanda: snapshot.IdFormalizacaoCriado ?? 0,
                        TotalMateriais: snapshot.TotalMateriais,
                        ValorTotal: valorTotalImp,
                        Sucesso: true,
                        MensagemErro: null,
                        LinhaErro: null,
                        Descricao: entrada.Descricao,
                        UsuarioLogin: origem.UsuarioLogin
                    ), ct).ConfigureAwait(false);
                }
                catch (Exception exHist)
                {
                    _log.LogError(exHist, "Falha ao registrar historico de importacao {Id}", idImp);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Importacao {Id} falhou", idImp);
            _registroImp.Atualizar(idImp, e =>
            {
                e.Estado = EstadoImportacao.Falhou;
                e.ConcluidaEm = DateTimeOffset.UtcNow;
                e.UltimoErro = ex.Message;
                e.Eventos.Add(new EventoImportacao(DateTimeOffset.UtcNow, "Erro", "Importação interrompida", ex.Message));
            });

            // Persiste registro de FALHA no historico (se a importação veio de link).
            (string IdPlanilha, string Url, string? UsuarioLogin) origemFalha;
            bool teveOrigemFalha;
            lock (_travaOrigem)
            {
                teveOrigemFalha = _origemDoLink.Remove(idImp, out origemFalha);
            }
            if (teveOrigemFalha)
            {
                try
                {
                    var snapshot = _registroImp.Obter(idImp)!;
                    var valorTotalImp = entrada.Materiais.Sum(m =>
                        (m.Quantidade ?? 0m) * (m.ValorUnitario ?? 0m));
                    await _historico.RegistrarAsync(new HistoricoImportacaoDto(
                        Id: 0,
                        IdPlanilha: origemFalha.IdPlanilha,
                        UrlOriginal: origemFalha.Url,
                        ImportadaEm: DateTimeOffset.UtcNow,
                        IdExecucao: idImp,
                        NumeroDfd: snapshot.NumeroDfd ?? 0,
                        AnoDfd: snapshot.AnoDfd ?? 0,
                        IdArtefato: snapshot.IdArtefatoCriado ?? 0,
                        IdFormalizacaoDemanda: snapshot.IdFormalizacaoCriado ?? 0,
                        TotalMateriais: snapshot.TotalMateriais,
                        ValorTotal: valorTotalImp,
                        Sucesso: false,
                        MensagemErro: ex.Message,
                        LinhaErro: linhaErroAtual,
                        Descricao: entrada.Descricao,
                        UsuarioLogin: origemFalha.UsuarioLogin
                    ), ct).ConfigureAwait(false);
                }
                catch (Exception exHist)
                {
                    _log.LogError(exHist, "Falha ao registrar historico de importacao {Id}", idImp);
                }
            }

            _logs.Registrar(NivelLog.Erro, "Importacao", $"[{idImp}] Falhou", ex.Message);
        }
    }

    public Task<IReadOnlyList<HistoricoImportacaoDto>> ListarHistoricoAsync(int limite = 200, CancellationToken ct = default) =>
        _historico.ListarAsync(limite, ct);

    private void ProgressoEtapa(string idImp, string etapa) =>
        _registroImp.Atualizar(idImp, e => e.EtapaAtual = etapa);
}
