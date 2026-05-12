namespace PcaImporter.Domain.Token;

public enum EstadoToken
{
    Ausente = 0,
    Saudavel = 1,
    ProximoExpirar = 2,
    Expirado = 3,
    RefreshFalhou = 4
}
