using System.Security.Cryptography;
using System.Text;

namespace OneC.Infrastructure.Security;

/// <summary>
///     Provides AES encryption/decryption for connection string passwords.
///     Not enterprise-grade: fixed key embedded in code by design.
/// </summary>
public static class ConnectionStringProtector
{
    // Fixed password embedded in code (not enterprise level, per project decision).
    // Key/IV derived via SHA256 to guarantee AES-valid sizes.
    private const string SecretPhrase = "OneC-Sync-2026-K3y!-Fixed-Secret";

    private static readonly byte[] Key = DeriveKey(SecretPhrase);
    private static readonly byte[] Iv = DeriveIv(SecretPhrase);

    private static byte[] DeriveKey(string phrase)
    {
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(Encoding.UTF8.GetBytes(phrase));
    }

    private static byte[] DeriveIv(string phrase)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(phrase));
        var iv = new byte[16];
        Array.Copy(hash, iv, iv.Length);
        return iv;
    }

    /// <summary>
    ///     Encrypts a plaintext password.
    /// </summary>
    /// <param name="plainText">Plaintext password.</param>
    /// <returns>Base64-encoded ciphertext.</returns>
    public static string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = Iv;

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(cipherBytes);
    }

    /// <summary>
    ///     Decrypts a Base64-encoded ciphertext.
    /// </summary>
    /// <param name="cipherText">Base64-encoded ciphertext.</param>
    /// <returns>Plaintext password.</returns>
    public static string Decrypt(string cipherText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cipherText);

        using var aes = Aes.Create();
        aes.Key = Key;
        aes.IV = Iv;

        using var decryptor = aes.CreateDecryptor();
        var cipherBytes = Convert.FromBase64String(cipherText);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

        return Encoding.UTF8.GetString(plainBytes);
    }
}