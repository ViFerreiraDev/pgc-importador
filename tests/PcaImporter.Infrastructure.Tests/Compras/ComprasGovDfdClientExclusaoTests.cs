using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Token;
using PcaImporter.Domain.Token;
using PcaImporter.Infrastructure.Compras;
using PcaImporter.Infrastructure.Compras.Dfd;

namespace PcaImporter.Infrastructure.Tests.Compras;

public sealed class ComprasGovDfdClientExclusaoTests
{
    [Fact]
    public async Task ExcluirDfdAsync_UsaIdArtefatoETokenMantidoPelaAplicacao()
    {
        HttpMethod? metodo = null;
        string? caminho = null;
        string? autorizacao = null;
        string? corpo = null;
        string? referer = null;

        using var http = new HttpClient(new HandlerDelegado(async (req, _) =>
        {
            metodo = req.Method;
            caminho = req.RequestUri?.PathAndQuery;
            autorizacao = req.Headers.Authorization?.ToString()
                ?? req.Headers.GetValues("authorization").Single();
            corpo = await req.Content!.ReadAsStringAsync();
            referer = req.Headers.Referrer?.ToString()
                ?? req.Headers.GetValues("referer").Single();

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }))
        {
            BaseAddress = new Uri("https://cnetmobile.estaleiro.serpro.gov.br"),
        };

        var cliente = new ComprasGovDfdClient(
            http,
            new GerenciadorTokenFake("token-mantido-vivo"),
            Options.Create(new ComprasGovOptions
            {
                BaseUrl = "https://cnetmobile.estaleiro.serpro.gov.br",
            }),
            NullLogger<ComprasGovDfdClient>.Instance,
            TimeProvider.System);

        var resultado = await cliente.ExcluirDfdAsync(5_868_919);

        Assert.Equal(HttpMethod.Post, metodo);
        Assert.Equal("/comprasnet-artefatos/api/v1/artefato/excluir/5868919", caminho);
        Assert.Equal("Bearer token-mantido-vivo", autorizacao);
        Assert.Equal("\"\"", corpo);
        Assert.Equal(
            "https://cnetmobile.estaleiro.serpro.gov.br/comprasnet-artefatos-web/artefatos/lista/DFD",
            referer);
        Assert.True(resultado.Excluido);
        Assert.Equal(5_868_919, resultado.IdArtefato);
    }

    private sealed class GerenciadorTokenFake(string accessToken) : IGerenciadorTokenSessao
    {
        public event Action<StatusTokenDto>? EstadoMudou
        {
            add { }
            remove { }
        }

        public StatusTokenDto ObterStatus() => throw new NotSupportedException();

        public Task<StatusTokenDto> DefinirAPartirDoRefreshAsync(
            string refreshToken, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<TokenSessao> ObterTokenValidoAsync(CancellationToken ct = default) => Task.FromResult(new TokenSessao(
            AccessToken: accessToken,
            RefreshToken: "refresh-fake",
            EmitidoEm: DateTimeOffset.UtcNow,
            ExpiraEm: DateTimeOffset.UtcNow.AddMinutes(10),
            Sub: "teste",
            IdSessao: 1,
            NumeroUasg: 1,
            Mnemonicos: [],
            TipoAutenticacao: "L"));

        public Task<StatusTokenDto> ForcarRefreshAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<bool> RefreshSeNecessarioAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public void Limpar() => throw new NotSupportedException();
    }

    private sealed class HandlerDelegado(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
