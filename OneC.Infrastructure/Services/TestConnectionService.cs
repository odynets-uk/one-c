using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneC.Application.Abstractions.Services;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Security;

namespace OneC.Infrastructure.Services;

/// <summary>
///     Tests the 1C COM connection and returns platform/configuration versions.
/// </summary>
public sealed class TestConnectionService : ITestConnectionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TestConnectionService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TestConnectionService" /> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public TestConnectionService(IConfiguration configuration, ILogger<TestConnectionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<TestConnectionResult> TestAsync(CancellationToken cancellationToken = default)
    {
        var connectionStringName = _configuration["ActiveConnection"] ?? "Kplus";
        var connectionString = _configuration.GetConnectionString(connectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var error = $"Connection string '{connectionStringName}' is not configured.";
            _logger.LogError("{Error}", error);
            throw new InvalidOperationException(error);
        }

        var decrypted = DecryptPassword(connectionString);

        using var session = new ComSession(
            new ComConnector(Logging.CreateLogger<ComConnector>()),
            Logging.CreateLogger<ComSession>());

        session.Connect(decrypted);

        var success = session.TestConnection();

        if (!success)
        {
            var error = "Test query failed. Connection to 1C base is not working.";
            _logger.LogError("{Error}", error);
            throw new InvalidOperationException(error);
        }

        _logger.LogInformation("Connection test successful.");

        return Task.FromResult(new TestConnectionResult("OK", "OK"));
    }

    private static string DecryptPassword(string connectionString)
    {
        // Connection string format: File="...";Usr="...";Pwd="...";
        // Password may be encrypted (AES). Decrypt if it matches Base64 pattern.
        const string pwdMarker = "Pwd=\"";

        var idx = connectionString.IndexOf(pwdMarker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return connectionString;
        }

        var start = idx + pwdMarker.Length;
        var end = connectionString.IndexOf('"', start);
        if (end < 0)
        {
            return connectionString;
        }

        var pwd = connectionString[start..end];

        // Try to decrypt only if it looks like Base64 (encrypted marker).
        // Plain passwords with digits only (e.g. "4715") are not encrypted.
        if (LooksLikeEncrypted(pwd))
        {
            try
            {
                var decrypted = ConnectionStringProtector.Decrypt(pwd);
                return connectionString[..start] + decrypted + connectionString[end..];
            }
            catch (FormatException)
            {
                // Not a valid Base64/AES value — treat as plain text.
            }
        }

        return connectionString;
    }

    private static bool LooksLikeEncrypted(string value)
    {
        // Encrypted AES output is Base64 — contains chars outside digits.
        return value.Any(c => char.IsLetter(c) && !char.IsWhiteSpace(c));
    }
}