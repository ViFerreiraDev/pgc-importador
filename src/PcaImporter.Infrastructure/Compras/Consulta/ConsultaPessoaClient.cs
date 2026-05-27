using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Consulta;
using PcaImporter.Application.Token;

namespace PcaImporter.Infrastructure.Compras.Consulta;

public sealed class ConsultaPessoaClient : IConsultaPessoaClient
{
    private readonly HttpClient _http;
    private readonly IGerenciadorTokenSessao _token;
    private readonly ComprasGovOptions _opcoes;
    private readonly ILogger<ConsultaPessoaClient> _log;

    public ConsultaPessoaClient(
        HttpClient http,
        IGerenciadorTokenSessao token,
        IOptions<ComprasGovOptions> opcoes,
        ILogger<ConsultaPessoaClient> log)
    {
        _http = http;
        _token = token;
        _opcoes = opcoes.Value;
        _log = log;
    }

    public async Task<ResultadoConsultaNome> ObterNomePorCpfAsync(string cpf, int? artefatoId, CancellationToken ct = default)
    {
        var apenasDigitos = new string((cpf ?? string.Empty).Where(char.IsDigit).ToArray());
        if (apenasDigitos.Length != 11)
        {
            return new ResultadoConsultaNome(false, 400, null, "CPF deve conter 11 dígitos.");
        }

        Domain.Token.TokenSessao sessao;
        try
        {
            sessao = await _token.ObterTokenValidoAsync(ct).ConfigureAwait(false);
        }
        catch (TokenIndisponivelException ex)
        {
            return new ResultadoConsultaNome(false, 503, null, $"Sessão Compras indisponível: {ex.Message}");
        }

        var idArtefato = artefatoId ?? _opcoes.Consulta.ArtefatoIdPadrao;
        var caminho = $"/comprasnet-artefatos/api/v1/artefato/{idArtefato}/nomeUsuarioPorCpfRFB?cpf={apenasDigitos}";

        using var req = new HttpRequestMessage(HttpMethod.Get, caminho);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessao.AccessToken);
        req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");

        try
        {
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            var corpo = (await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false))?.Trim() ?? string.Empty;

            if (resp.StatusCode == HttpStatusCode.NotFound)
            {
                return new ResultadoConsultaNome(false, 404, null, "CPF não encontrado na RFB.");
            }

            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Consulta CPF retornou {Status}. Corpo: {Corpo}", (int)resp.StatusCode, Truncar(corpo));
                return new ResultadoConsultaNome(false, (int)resp.StatusCode, null, $"HTTP {(int)resp.StatusCode}");
            }

            // Resposta é texto puro com o nome (eventualmente entre aspas se vier como JSON string)
            var nome = corpo.Trim('"', ' ', '\r', '\n', '\t');
            if (string.IsNullOrWhiteSpace(nome))
            {
                return new ResultadoConsultaNome(false, 204, null, "Resposta vazia.");
            }

            return new ResultadoConsultaNome(true, 200, nome, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao consultar CPF {Cpf} no Serpro", apenasDigitos);
            return new ResultadoConsultaNome(false, 502, null, ex.Message);
        }
    }

    private static string Truncar(string s, int max = 500) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "...[truncado]");
}
