using PcaImporter.Domain.Usuarios;

namespace PcaImporter.Infrastructure.Persistencia.Entidades;

public sealed class UsuarioEntity
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public Papel Papel { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public string? CriadoPorLogin { get; set; }
}
