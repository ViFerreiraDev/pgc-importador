using PcaImporter.Domain.Usuarios;

namespace PcaImporter.Application.Usuarios;

public sealed record UsuarioDto(
    int Id,
    string Login,
    string Nome,
    Papel Papel,
    DateTimeOffset CriadoEm,
    string? CriadoPorLogin
);

public sealed record CriarUsuarioInput(
    string Login,
    string Nome,
    string Senha,
    Papel Papel
);

public sealed record AlterarSenhaInput(string SenhaAtual, string NovaSenha);

public interface IServicoUsuarios
{
    Task<UsuarioDto?> AutenticarAsync(string login, string senha, CancellationToken ct = default);

    Task<IReadOnlyList<UsuarioDto>> ListarAsync(CancellationToken ct = default);

    Task<UsuarioDto?> ObterPorLoginAsync(string login, CancellationToken ct = default);

    Task<UsuarioDto> CriarAsync(CriarUsuarioInput input, string? criadoPorLogin, CancellationToken ct = default);

    Task<bool> AlterarSenhaAsync(int id, string novaSenha, CancellationToken ct = default);

    Task<bool> RemoverAsync(int id, CancellationToken ct = default);

    Task<bool> ExisteAlgumAdminAsync(CancellationToken ct = default);
}

public sealed class UsuarioJaExisteException : Exception
{
    public UsuarioJaExisteException(string login) : base($"Já existe um usuário com login '{login}'.") { }
}

public sealed class CredenciaisInvalidasException : Exception
{
    public CredenciaisInvalidasException() : base("Login ou senha inválidos.") { }
}
