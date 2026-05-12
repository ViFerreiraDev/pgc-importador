namespace PcaImporter.Infrastructure.Importacao;

/// <summary>
/// Política de retentativa simples com pausas escalonadas: 5s, 20s, 30s.
/// 4 tentativas no total (1 inicial + 3 retentativas). Falhou nas 4 → propaga a exceção.
/// </summary>
public static class Resiliencia
{
    public static readonly TimeSpan[] AtrasosPadrao =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(30),
    ];

    public static async Task<T> TentarComBackoffAsync<T>(
        Func<CancellationToken, Task<T>> acao,
        Action<int, Exception, TimeSpan>? aoFalhar = null,
        TimeSpan[]? atrasos = null,
        CancellationToken ct = default)
    {
        var pausas = atrasos ?? AtrasosPadrao;
        Exception? ultimo = null;

        for (var tentativa = 0; tentativa <= pausas.Length; tentativa++)
        {
            try
            {
                return await acao(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ultimo = ex;
                if (tentativa == pausas.Length)
                {
                    break;
                }
                aoFalhar?.Invoke(tentativa + 1, ex, pausas[tentativa]);
                await Task.Delay(pausas[tentativa], ct).ConfigureAwait(false);
            }
        }

        throw ultimo!;
    }

    /// <summary>Versão sem retorno (para chamadas que não retornam valor útil).</summary>
    public static Task TentarComBackoffAsync(
        Func<CancellationToken, Task> acao,
        Action<int, Exception, TimeSpan>? aoFalhar = null,
        TimeSpan[]? atrasos = null,
        CancellationToken ct = default)
        => TentarComBackoffAsync<bool>(async (c) => { await acao(c).ConfigureAwait(false); return true; },
            aoFalhar, atrasos, ct);
}
