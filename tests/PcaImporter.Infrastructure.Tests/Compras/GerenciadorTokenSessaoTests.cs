using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Logs;
using PcaImporter.Application.Token;
using PcaImporter.Infrastructure.Compras;

namespace PcaImporter.Infrastructure.Tests.Compras;

public sealed class GerenciadorTokenSessaoTests
{
    [Fact]
    public async Task ForcarRefreshAsync_Erro5xxEhRetentadoERotacaoEhPersistida()
    {
        var cliente = new ClienteTokenFalso();
        cliente.Enfileirar(Sucesso("refresh-inicial"));
        cliente.Enfileirar(Falha(503));
        cliente.Enfileirar(Falha(503));
        cliente.Enfileirar(Sucesso("refresh-rotacionado"));
        var repo = new RepositorioTokenFalso();
        using var gerenciador = CriarGerenciador(cliente, repo);
        await gerenciador.DefinirAPartirDoRefreshAsync("refresh-colado");

        var status = await gerenciador.ForcarRefreshAsync();

        Assert.Equal(4, cliente.Chamadas);
        Assert.Equal("refresh-rotacionado", repo.RefreshSalvo);
        Assert.Equal(0, status.FalhasConsecutivasRefresh);
        Assert.True(status.TemRefreshToken);
    }

    [Fact]
    public async Task ForcarRefreshAsync_401LimpaSessaoPersistida()
    {
        var cliente = new ClienteTokenFalso();
        cliente.Enfileirar(Sucesso("refresh-valido"));
        cliente.Enfileirar(Falha(401));
        var repo = new RepositorioTokenFalso();
        using var gerenciador = CriarGerenciador(cliente, repo);
        await gerenciador.DefinirAPartirDoRefreshAsync("refresh-colado");

        var status = await gerenciador.ForcarRefreshAsync();

        Assert.Equal(PcaImporter.Domain.Token.EstadoToken.Ausente, status.Estado);
        Assert.False(status.TemRefreshToken);
        Assert.Equal(1, repo.Limpezas);
        Assert.Contains("401", status.UltimoErroRefresh);
    }

    [Fact]
    public async Task RefreshConcorrenteEhSingleFlight()
    {
        var cliente = new ClienteTokenFalso(atraso: TimeSpan.FromMilliseconds(50));
        cliente.Enfileirar(Sucesso("refresh-inicial"));
        cliente.Enfileirar(Sucesso("refresh-a"));
        cliente.Enfileirar(Sucesso("refresh-b"));
        var repo = new RepositorioTokenFalso();
        using var gerenciador = CriarGerenciador(cliente, repo);
        await gerenciador.DefinirAPartirDoRefreshAsync("refresh-colado");

        await Task.WhenAll(gerenciador.ForcarRefreshAsync(), gerenciador.ForcarRefreshAsync());

        Assert.Equal(1, cliente.MaximoConcorrente);
        Assert.Equal("refresh-b", repo.RefreshSalvo);
    }

    private static GerenciadorTokenSessao CriarGerenciador(
        IComprasGovTokenClient cliente,
        IRepositorioTokenSessao repo)
    {
        var opcoes = Options.Create(new ComprasGovOptions
        {
            Token = new ComprasGovOptions.TokenOptions
            {
                TentativasRefresh = 3,
                BackoffMinimoSegundos = 0,
                BackoffMaximoSegundos = 0
            }
        });
        return new GerenciadorTokenSessao(
            cliente,
            repo,
            new RegistroLogsFalso(),
            opcoes,
            NullLogger<GerenciadorTokenSessao>.Instance,
            TimeProvider.System);
    }

    private static RespostaRefreshToken Sucesso(string refresh) =>
        new(true, 200, CriarJwtValido(), refresh, "{}", null);

    private static RespostaRefreshToken Falha(int status) =>
        new(false, status, null, null, string.Empty, $"HTTP {status}");

    private static string CriarJwtValido()
    {
        var agora = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = "teste",
            id_sessao = 123L,
            numero_uasg = 456,
            iat = agora,
            exp = agora + 600,
            autenticacao = "teste",
            mnemonicos = new[] { "TESTE" }
        });
        return $"{Base64Url(Encoding.UTF8.GetBytes("{}"))}.{Base64Url(payload)}.assinatura";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class ClienteTokenFalso(TimeSpan? atraso = null) : IComprasGovTokenClient
    {
        private readonly Queue<RespostaRefreshToken> _respostas = new();
        private int _concorrentes;

        public int Chamadas { get; private set; }
        public int MaximoConcorrente { get; private set; }

        public void Enfileirar(RespostaRefreshToken resposta) => _respostas.Enqueue(resposta);

        public async Task<RespostaRefreshToken> RefreshAsync(string refreshTokenAtual, CancellationToken ct = default)
        {
            Chamadas++;
            var concorrentes = Interlocked.Increment(ref _concorrentes);
            MaximoConcorrente = Math.Max(MaximoConcorrente, concorrentes);
            try
            {
                if (atraso is { } espera) await Task.Delay(espera, ct);
                return _respostas.Dequeue();
            }
            finally
            {
                Interlocked.Decrement(ref _concorrentes);
            }
        }
    }

    private sealed class RepositorioTokenFalso : IRepositorioTokenSessao
    {
        public string? RefreshSalvo { get; private set; }
        public int Limpezas { get; private set; }

        public Task<string?> LerRefreshAsync(CancellationToken ct = default) =>
            Task.FromResult(RefreshSalvo);

        public Task SalvarRefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            RefreshSalvo = refreshToken;
            return Task.CompletedTask;
        }

        public Task LimparAsync(CancellationToken ct = default)
        {
            RefreshSalvo = null;
            Limpezas++;
            return Task.CompletedTask;
        }
    }

    private sealed class RegistroLogsFalso : IRegistroLogs
    {
        public void Registrar(NivelLog nivel, string categoria, string mensagem,
            string? detalhes = null, string? usuarioLogin = null)
        {
        }

        public PaginaLogsDto Consultar(int pagina, int tamanhoPagina,
            NivelLog? nivelMinimo = null, string? categoria = null) =>
            new([], pagina, tamanhoPagina, 0);
    }
}
