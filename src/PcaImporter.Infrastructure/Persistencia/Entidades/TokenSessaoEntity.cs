namespace PcaImporter.Infrastructure.Persistencia.Entidades;

public sealed class TokenSessaoEntity
{
    public const int IdFixo = 1;

    public int Id { get; set; }

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset AtualizadoEm { get; set; }
}
