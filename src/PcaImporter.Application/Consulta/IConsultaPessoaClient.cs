namespace PcaImporter.Application.Consulta;

/// <summary>
/// Resolve dados de pessoa física a partir do CPF, usando o token do Compras
/// que o servidor mantém vivo (refresh automático via KeepAliveTokenWorker).
/// </summary>
public interface IConsultaPessoaClient
{
    /// <param name="cpf">CPF apenas com dígitos (11 caracteres).</param>
    /// <param name="artefatoId">Id de artefato usado no path do gateway Serpro.
    /// Qualquer artefato válido autoriza a chamada — usa o padrão configurado se nulo.</param>
    Task<ResultadoConsultaNome> ObterNomePorCpfAsync(string cpf, int? artefatoId, CancellationToken ct = default);
}

public sealed record ResultadoConsultaNome(
    bool Sucesso,
    int StatusHttp,
    string? Nome,
    string? Erro);
