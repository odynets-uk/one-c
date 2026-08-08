using OneC.Infrastructure.Security;
using Xunit;

namespace OneC.Tests;

/// <summary>
///     Tests for <see cref="ConnectionStringProtector" />.
/// </summary>
public class ConnectionStringProtectorTests
{
    [Fact]
    public void Encrypt_ThenDecrypt_ReturnsOriginalPassword()
    {
        const string password = "4715";

        var encrypted = ConnectionStringProtector.Encrypt(password);
        var decrypted = ConnectionStringProtector.Decrypt(encrypted);

        Assert.NotEqual(password, encrypted);
        Assert.Equal(password, decrypted);
    }

    [Fact]
    public void Encrypt_ProducesBase64()
    {
        var encrypted = ConnectionStringProtector.Encrypt("secret123");

        // AES output is Base64 — should not contain the original text.
        Assert.DoesNotContain("secret123", encrypted);
        Assert.True(IsBase64(encrypted));
    }

    [Fact]
    public void Encrypt_SameInput_ProducesDifferentCiphertext()
    {
        var first = ConnectionStringProtector.Encrypt("same-password");
        var second = ConnectionStringProtector.Encrypt("same-password");

        // Fixed IV means same output; key/IV are constant by design.
        Assert.Equal(first, second);
    }

    [Fact]
    public void Decrypt_InvalidBase64_Throws()
    {
        Assert.Throws<FormatException>(() => ConnectionStringProtector.Decrypt("not-valid-base64!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Encrypt_EmptyInput_Throws(string password)
    {
        Assert.Throws<ArgumentException>(() => ConnectionStringProtector.Encrypt(password));
    }

    [Fact]
    public void Encrypt_NullInput_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => ConnectionStringProtector.Encrypt(null!));
    }

    private static bool IsBase64(string value)
    {
        try
        {
            _ = Convert.FromBase64String(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}