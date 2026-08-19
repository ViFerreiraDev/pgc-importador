using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PcaImporter.Application.Token;
using PcaImporter.Infrastructure.Compras;

namespace PcaImporter.Infrastructure.Tests.Compras;

public sealed class KeepAliveTokenWorkerTests
{
    [Fact]
    public async Task ExecutarCicloAsync_CancelamentoTransitorioNaoMataWorker()
    {
        var gerenciador = new GerenciadorFalso { CancelarProximoCiclo = true };
        var worker = CriarWorker(gerenciador);

        await worker.ExecutarCicloAsync(CancellationToken.None);
        await worker.ExecutarCicloAsync(CancellationToken.None);

        Assert.Equal(2, gerenciador.Ciclos);
        Assert.NotNull(worker.UltimoHeartbeatEm);
    }

    [Fact]
    public async Task ExecutarCicloAsync_CancelamentoDoHostEncerraCiclo()
    {
        var gerenciador = new GerenciadorFalso { CancelarProximoCiclo = true };
        var worker = CriarWorker(gerenciador);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            worker.ExecutarCicloAsync(cts.Token));
    }

    private static KeepAliveTokenWorker CriarWorker(IGerenciadorTokenSessao gerenciador) =>
        new(
            gerenciador,
            Options.Create(new ComprasGovOptions()),
            NullLogger<KeepAliveTokenWorker>.Instance,
            TimeProvider.System);

    private sealed class GerenciadorFalso : IGerenciadorTokenSessao
    {
        public bool CancelarProximoCiclo { get; set; }
        public int Ciclos { get; private set; }

        public event Action<StatusTokenDto>? EstadoMudou;

        public Task<bool> RefreshSeNecessarioAsync(CancellationToken ct = default)
        {
            Ciclos++;
            if (CancelarProximoCiclo)
            {
                CancelarProximoCiclo = false;
                throw new OperationCanceledException(ct);
            }
            return Task.FromResult(true);
        }

        public StatusTokenDto ObterStatus() =>
            new(PcaImporter.Domain.Token.EstadoToken.Ausente, null, null, null, null, null,
                null, null, null, null, false);

        public Task<StatusTokenDto> DefinirAPartirDoRefreshAsync(string refreshToken, CancellationToken ct = default) =>
            Task.FromResult(ObterStatus());

        public Task<PcaImporter.Domain.Token.TokenSessao> ObterTokenValidoAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<StatusTokenDto> ForcarRefreshAsync(CancellationToken ct = default) =>
            Task.FromResult(ObterStatus());

        public void Limpar() => EstadoMudou?.Invoke(ObterStatus());
    }
}
