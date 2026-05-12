using System.Security.Cryptography;

namespace PcaImporter.Infrastructure.Usuarios;

/// <summary>
/// PBKDF2-SHA256 + salt aleatório de 16 bytes, 100k iterações, 32 bytes de saída.
/// Sem dependência externa — usa apenas BCL.
/// </summary>
public static class HasherSenha
{
    private const int IteracoesPbkdf2 = 100_000;
    private const int TamanhoHash = 32;
    private const int TamanhoSalt = 16;

    public static (string Hash, string Salt) Gerar(string senha)
    {
        var salt = RandomNumberGenerator.GetBytes(TamanhoSalt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, salt, IteracoesPbkdf2, HashAlgorithmName.SHA256, TamanhoHash);
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool Verificar(string senha, string hashEsperado, string saltBase64)
    {
        try
        {
            var salt = Convert.FromBase64String(saltBase64);
            var hashCalculado = Rfc2898DeriveBytes.Pbkdf2(senha, salt, IteracoesPbkdf2, HashAlgorithmName.SHA256, TamanhoHash);
            var esperado = Convert.FromBase64String(hashEsperado);
            return CryptographicOperations.FixedTimeEquals(hashCalculado, esperado);
        }
        catch
        {
            return false;
        }
    }
}
