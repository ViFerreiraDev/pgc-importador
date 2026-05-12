using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Logs;
using PcaImporter.Application.Token;
using PcaImporter.Domain.Token;

namespace PcaImporter.Infrastructure.Compras;

public sealed class GerenciadorTokenSessao : IGerenciadorTokenSessao, IDisposable
{
    private readonly IComprasGovTokenClient _client;
    private readonly IRepositorioTokenSessao _repo;
    private readonly IRegistroLogs _logsApp;
    private readonly ComprasGovOptions _opcoes;
    private readonly ILogger<GerenciadorTokenSessao> _log;
    private readonly TimeProvider _tempo;
    private readonly SemaphoreSlim _sem = new(1, 1);

    private TokenSessao? _atual;
    private DateTimeOffset? _ultimoRefreshEm;
    private string? _ultimoErro;

    public event Action<StatusTokenDto>? EstadoMudou;

    public GerenciadorTokenSessao(
        IComprasGovTokenClient client,
        IRepositorioTokenSessao repo,
        IRegistroLogs logsApp,
        IOptions<ComprasGovOptions> opcoes,
        ILogger<GerenciadorTokenSessao> log,
        TimeProvider tempo)
    {
        _client = client;
        _repo = repo;
        _logsApp = logsApp;
        _opcoes = opcoes.Value;
        _log = log;
        _tempo = tempo;
    }

    public StatusTokenDto ObterStatus()
    {
        var agora = _tempo.GetUtcNow();
        return MontarStatus(agora);
    }

    public async Task<StatusTokenDto> DefinirAPartirDoRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("Refresh token obrigatorio.", nameof(refreshToken));
        }

        var refreshNorm = JwtDecoder.NormalizarJwt(refreshToken);

        var resp = await _client.RefreshAsync(refreshNorm, ct).ConfigureAwait(false);
        if (!resp.Sucesso || string.IsNullOrWhiteSpace(resp.NovoAccessToken))
        {
            var msg = resp.Erro ?? $"Refresh inicial falhou (HTTP {resp.StatusHttp})";
            _log.LogWarning("Bootstrap a partir do refresh falhou: {Msg}", msg);
            throw new RefreshInicialFalhouException(resp.StatusHttp, resp.CorpoBruto, msg);
        }

        var refreshFinal = !string.IsNullOrWhiteSpace(resp.NovoRefreshToken) ? resp.NovoRefreshToken : refreshNorm;
        var token = JwtDecoder.Decodificar(resp.NovoAccessToken, refreshFinal);

        await _sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _atual = token;
            _ultimoErro = null;
            _ultimoRefreshEm = _tempo.GetUtcNow();
            _log.LogInformation("Token bootstrapado a partir do refresh. Sub={Sub} IdSessao={IdSessao} ExpiraEm={Exp}",
                token.Sub, token.IdSessao, token.ExpiraEm);

            _logsApp.Registrar(NivelLog.Sucesso, "Token", "Sessão iniciada", $"sub={token.Sub} uasg={token.NumeroUasg} expira={token.ExpiraEm:HH:mm:ss}");

            if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            {
                try { await _repo.SalvarRefreshAsync(token.RefreshToken, ct).ConfigureAwait(false); }
                catch (Exception ex) { _log.LogError(ex, "Falha ao persistir refresh token"); }
            }

            DispararEstado();
            return MontarStatus(_tempo.GetUtcNow());
        }
        finally
        {
            _sem.Release();
        }
    }

    public async Task<TokenSessao> ObterTokenValidoAsync(CancellationToken ct = default)
    {
        await _sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var agora = _tempo.GetUtcNow();
            if (_atual is null)
            {
                throw new TokenIndisponivelException("Nenhum token definido.");
            }

            if (_atual.EstaExpirado(agora))
            {
                _log.LogWarning("Token expirado, tentando refresh sob demanda.");
                var ok = await TentarRefreshSemTrava(ct).ConfigureAwait(false);
                if (!ok || _atual is null || _atual.EstaExpirado(_tempo.GetUtcNow()))
                {
                    throw new TokenIndisponivelException("Token expirado e refresh nao recuperou. Necessario novo bearer.");
                }
                return _atual;
            }

            var restante = _atual.TempoRestante(agora);
            if (restante.TotalSeconds <= _opcoes.Token.LimiarRefreshSegundos)
            {
                _log.LogInformation("Token proximo de expirar (restante {Seg}s), refresh proativo.", (long)restante.TotalSeconds);
                _ = await TentarRefreshSemTrava(ct).ConfigureAwait(false);
            }

            return _atual!;
        }
        finally
        {
            _sem.Release();
        }
    }

    public async Task<StatusTokenDto> ForcarRefreshAsync(CancellationToken ct = default)
    {
        await _sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await TentarRefreshSemTrava(ct).ConfigureAwait(false);
            return MontarStatus(_tempo.GetUtcNow());
        }
        finally
        {
            _sem.Release();
        }
    }

    public async Task<bool> RefreshSeNecessarioAsync(CancellationToken ct = default)
    {
        await _sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_atual is null) return false;
            var agora = _tempo.GetUtcNow();
            if (_atual.EstaExpirado(agora))
            {
                return await TentarRefreshSemTrava(ct).ConfigureAwait(false);
            }
            var restante = _atual.TempoRestante(agora);
            if (restante.TotalSeconds <= _opcoes.Token.LimiarRefreshSegundos)
            {
                return await TentarRefreshSemTrava(ct).ConfigureAwait(false);
            }

            DispararEstado();
            return true;
        }
        finally
        {
            _sem.Release();
        }
    }

    public void Limpar()
    {
        _sem.Wait();
        try
        {
            _atual = null;
            _ultimoErro = null;
            _ultimoRefreshEm = null;
            try { _repo.LimparAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { _log.LogError(ex, "Falha ao limpar refresh persistido"); }
            _logsApp.Registrar(NivelLog.Info, "Token", "Sessão encerrada");
            DispararEstado();
        }
        finally
        {
            _sem.Release();
        }
    }

    public void Dispose() => _sem.Dispose();

    private async Task<bool> TentarRefreshSemTrava(CancellationToken ct)
    {
        if (_atual is null) return false;

        if (string.IsNullOrWhiteSpace(_atual.RefreshToken))
        {
            _ultimoErro = "Nenhum refresh token armazenado. Cole o par accessToken+refreshToken.";
            _log.LogWarning(_ultimoErro);
            DispararEstado();
            return false;
        }

        var resp = await _client.RefreshAsync(_atual.RefreshToken, ct).ConfigureAwait(false);
        _ultimoRefreshEm = _tempo.GetUtcNow();

        if (resp.Sucesso && !string.IsNullOrWhiteSpace(resp.NovoAccessToken))
        {
            try
            {
                var refreshFinal = !string.IsNullOrWhiteSpace(resp.NovoRefreshToken)
                    ? resp.NovoRefreshToken
                    : _atual.RefreshToken;

                var novo = JwtDecoder.Decodificar(resp.NovoAccessToken, refreshFinal);
                _atual = novo;
                _ultimoErro = null;
                _log.LogInformation("Refresh OK. Novo exp: {Exp}. RefreshTokenRotacionado={Rot}",
                    novo.ExpiraEm, !string.IsNullOrWhiteSpace(resp.NovoRefreshToken));

                _logsApp.Registrar(NivelLog.Info, "Token", "Token renovado", $"novo expira={novo.ExpiraEm:HH:mm:ss}");

                if (!string.IsNullOrWhiteSpace(novo.RefreshToken))
                {
                    try { await _repo.SalvarRefreshAsync(novo.RefreshToken, ct).ConfigureAwait(false); }
                    catch (Exception ex) { _log.LogError(ex, "Falha ao persistir refresh apos rotacao"); }
                }

                DispararEstado();
                return true;
            }
            catch (Exception ex)
            {
                _ultimoErro = $"Refresh respondeu mas access token retornado invalido: {ex.Message}";
                _log.LogError(ex, "Access token retornado pelo refresh nao decodificou.");
                DispararEstado();
                return false;
            }
        }

        _ultimoErro = resp.Erro ?? $"Refresh falhou (HTTP {resp.StatusHttp})";
        _logsApp.Registrar(NivelLog.Erro, "Token", "Falha ao renovar sessão", _ultimoErro);
        DispararEstado();
        return false;
    }

    private void DispararEstado()
    {
        var status = MontarStatus(_tempo.GetUtcNow());
        try
        {
            EstadoMudou?.Invoke(status);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erro ao notificar EstadoMudou");
        }
    }

    private StatusTokenDto MontarStatus(DateTimeOffset agora)
    {
        if (_atual is null)
        {
            return new StatusTokenDto(EstadoToken.Ausente, null, null, null, null, null, null, null, _ultimoRefreshEm, _ultimoErro, false);
        }

        var restante = _atual.TempoRestante(agora);
        var segundos = (long)restante.TotalSeconds;

        EstadoToken estado;
        if (segundos <= 0)
        {
            estado = string.IsNullOrEmpty(_ultimoErro) ? EstadoToken.Expirado : EstadoToken.RefreshFalhou;
        }
        else if (segundos <= _opcoes.Token.LimiarRefreshSegundos)
        {
            estado = string.IsNullOrEmpty(_ultimoErro) ? EstadoToken.ProximoExpirar : EstadoToken.RefreshFalhou;
        }
        else
        {
            estado = EstadoToken.Saudavel;
        }

        return new StatusTokenDto(
            Estado: estado,
            EmitidoEm: _atual.EmitidoEm,
            ExpiraEm: _atual.ExpiraEm,
            SegundosRestantes: segundos,
            Sub: _atual.Sub,
            IdSessao: _atual.IdSessao,
            NumeroUasg: _atual.NumeroUasg,
            Mnemonicos: _atual.Mnemonicos,
            UltimoRefreshEm: _ultimoRefreshEm,
            UltimoErroRefresh: _ultimoErro,
            TemRefreshToken: !string.IsNullOrWhiteSpace(_atual.RefreshToken)
        );
    }
}
