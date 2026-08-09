using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneC.Application.Abstractions.Services;
using OneC.Domain.Profiles;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Profiles;
using OneC.Infrastructure.Readers;
using OneC.Infrastructure.Security;

namespace OneC.Infrastructure.Services;

/// <summary>
///     Reads catalog data from 1C and outputs it to JSON according to the profile.
/// </summary>
public sealed class GetCatalogService : IGetCatalogService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<GetCatalogService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GetCatalogService" /> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public GetCatalogService(IConfiguration configuration, ILogger<GetCatalogService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<int> ExecuteAsync(
        ExtractionProfile profile,
        int batchSize = -1,
        CancellationToken cancellationToken = default)
    {
        if (batchSize < 0)
        {
            batchSize = profile.BatchSize;
        }

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

        var mapper = new ComValueMapper(Logging.CreateLogger<ComValueMapper>());
        var reader = new CatalogReader(session, mapper, Logging.CreateLogger<CatalogReader>());

        var records = reader.Read(profile, batchSize);

        WriteJsonOutput(profile, records);

        _logger.LogInformation(
            "Catalog '{ProfileName}' extracted: {Count} records.",
            profile.Name,
            records.Count);

        return Task.FromResult(records.Count);
    }

    private void WriteJsonOutput(ExtractionProfile profile, IReadOnlyList<Dictionary<string, object?>> records)
    {
        var filePath = profile.Output?.Json?.FilePath ?? profile.Output?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        filePath = ExpandDatePlaceholder(filePath);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var pretty = profile.Output?.Json?.Pretty ?? profile.Output?.Pretty ?? false;
        var options = new JsonSerializerOptions
        {
            WriteIndented = pretty,
        };

        var json = JsonSerializer.Serialize(records, options);
        File.WriteAllText(filePath, json);

        _logger.LogInformation("Written {Count} records to {FilePath}.", records.Count, filePath);
    }

    private static string ExpandDatePlaceholder(string path)
    {
        return path.Replace("{date}", DateTime.Now.ToString("yyyy-MM-dd"), StringComparison.Ordinal);
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
        return value.Any(c => char.IsLetter(c) && !char.IsWhiteSpace(c));
    }
}