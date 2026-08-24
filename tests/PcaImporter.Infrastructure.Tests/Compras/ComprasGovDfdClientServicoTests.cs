using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Compras.Dfd;
using PcaImporter.Application.Token;
using PcaImporter.Domain.Token;
using PcaImporter.Infrastructure.Compras;
using PcaImporter.Infrastructure.Compras.Dfd;

namespace PcaImporter.Infrastructure.Tests.Compras;

public sealed class ComprasGovDfdClientServicoTests
{
    [Fact]
    public async Task AdicionarServicoAsync_EnviaPayloadCatserERefererDoDfd()
    {
        HttpMethod? metodo = null;
        string? caminho = null;
        string? referer = null;
        string? corpo = null;

        using var http = new HttpClient(new HandlerDelegado(async (req, _) =>
        {
            metodo = req.Method;
            caminho = req.RequestUri?.PathAndQuery;
            referer = req.Headers.Referrer?.ToString()
                ?? req.Headers.GetValues("referer").Single();
            corpo = await req.Content!.ReadAsStringAsync();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"id":1,"idFormalizacaoDemanda":2266559,"tipo":"SERVICO","codigo":"5380","siglaUnidadeMedida":"UN","quantidade":1,"valorUnitario":2,"valorTotal":2,"moeda":"Real"}"""),
            };
        }))
        {
            BaseAddress = new Uri("https://cnetmobile.estaleiro.serpro.gov.br"),
        };

        var cliente = new ComprasGovDfdClient(
            http,
            new GerenciadorTokenFake("token-teste"),
            Options.Create(new ComprasGovOptions
            {
                BaseUrl = "https://cnetmobile.estaleiro.serpro.gov.br",
            }),
            NullLogger<ComprasGovDfdClient>.Instance,
            TimeProvider.System);

        var resultado = await cliente.AdicionarServicoAsync(
            idArtefato: 5_888_811,
            numero: 456,
            ano: 2026,
            new ServicoInput(
                Codigo: "5380",
                Tipo: "SERVICO",
                SiglaUnidadeMedida: "UN",
                Descricao: "Prestação de Serviços de Apoio Administrativo, Técnico e Operacional",
                ValorUnitario: 2,
                Moeda: "Real",
                NomeGrupo: "Serviços Administrativos Do Governo",
                Quantidade: 1,
                IdFormalizacaoDemanda: 2_266_559,
                IdGrupo: 911));

        Assert.Equal(HttpMethod.Post, metodo);
        Assert.Equal("/comprasnet-artefatos/api/v1/artefato/dfd/materialservico", caminho);
        Assert.Equal(
            "https://cnetmobile.estaleiro.serpro.gov.br/comprasnet-artefatos-web/artefatos/edit/5888811?artefato=456%2F2026&tipo=DFD",
            referer);

        using var json = JsonDocument.Parse(corpo!);
        var raiz = json.RootElement;
        Assert.Equal("5380", raiz.GetProperty("codigo").GetString());
        Assert.Equal("SERVICO", raiz.GetProperty("tipo").GetString());
        Assert.Equal("UN", raiz.GetProperty("siglaUnidadeMedida").GetString());
        Assert.Equal(911, raiz.GetProperty("idGrupo").GetInt32());
        Assert.Equal("Serviços Administrativos Do Governo", raiz.GetProperty("nomeGrupo").GetString());
        Assert.False(raiz.TryGetProperty("idClasse", out _));
        Assert.False(raiz.TryGetProperty("nomeClasse", out _));
        Assert.False(raiz.TryGetProperty("siglaUnidadeFornecimento", out _));
        Assert.False(raiz.TryGetProperty("idPadraoDescritivo", out _));

        Assert.Equal("UN", resultado.SiglaUnidadeFornecimento);
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
