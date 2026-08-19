using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PcaImporter.Infrastructure.Compras;

namespace PcaImporter.Infrastructure.Tests.Compras;

public sealed class ComprasGovTokenClientTests
{
    [Fact]
    public async Task RefreshAsync_TimeoutEhFalhaTransitoriaEmVezDeCancelamento()
    {
        using var http = CriarHttpClient(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("inalcancavel");
        });
        var cliente = CriarCliente(http, timeoutSegundos: 1);

        var resposta = await cliente.RefreshAsync("aaaaa.bbbbb.ccccc");

        Assert.False(resposta.Sucesso);
        Assert.Equal(0, resposta.StatusHttp);
        Assert.Contains("Timeout", resposta.Erro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RefreshAsync_CancelamentoDoHostEhPropagado()
    {
        using var http = CriarHttpClient(async (_, ct) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("inalcancavel");
        });
        var cliente = CriarCliente(http, timeoutSegundos: 30);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cliente.RefreshAsync("aaaaa.bbbbb.ccccc", cts.Token));
    }

    private static ComprasGovTokenClient CriarCliente(HttpClient http, int timeoutSegundos)
    {
        var opcoes = Options.Create(new ComprasGovOptions
        {
            Token = new ComprasGovOptions.TokenOptions
            {
                TimeoutRetokenSegundos = timeoutSegundos
            }
        });
        return new ComprasGovTokenClient(http, opcoes, NullLogger<ComprasGovTokenClient>.Instance);
    }

    private static HttpClient CriarHttpClient(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        return new HttpClient(new HandlerDelegado(responder))
        {
            BaseAddress = new Uri("https://compras.gov.test"),
            Timeout = Timeout.InfiniteTimeSpan
        };
    }

    private sealed class HandlerDelegado(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
