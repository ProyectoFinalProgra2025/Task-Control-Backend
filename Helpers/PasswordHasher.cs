using System.Security.Cryptography;

namespace TaskControlBackend.Helpers;

public static class PasswordHasher
{
    private const int SaltSize = 16;      // 128 bits
    private const int KeySize = 32;       // 256 bits
    private const int Iterations = 100_000;

    public static void CreatePasswordHash(string password, out byte[] hash, out byte[] salt)
    {
        salt = RandomNumberGenerator.GetBytes(SaltSize);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
        hash = pbkdf2.GetBytes(KeySize);
    }

    public static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, storedSalt, Iterations, HashAlgorithmName.SHA256);
        var computed = pbkdf2.GetBytes(KeySize);
        return CryptographicOperations.FixedTimeEquals(storedHash, computed);
    }
}