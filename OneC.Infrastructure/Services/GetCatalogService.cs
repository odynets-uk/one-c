using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneC.Application.Abstractions.Services;
using OneC.Domain.Profiles;
using OneC.Infrastructure.Com;
using OneC.Infrastructure.Json;
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

        var mapper = new ComValueMapper(session, Logging.CreateLogger<ComValueMapper>());
        var reader = new CatalogReader(session, mapper, Logging.CreateLogger<CatalogReader>());

        var stopwatch = Stopwatch.StartNew();
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;
        var memBefore = Process.GetCurrentProcess().WorkingSet64;

        var records = reader.Read(profile, batchSize);

        WriteJsonOutput(profile, records, batchSize);

        stopwatch.Stop();
        var cpuAfter = Process.GetCurrentProcess().TotalProcessorTime;
        var memAfter = Process.GetCurrentProcess().WorkingSet64;

        _logger.LogInformation(
            "Catalog '{ProfileName}' extracted: {Count} records in {ElapsedMs} ms (CPU {CpuMs} ms, RAM {RamDeltaMb} MB).",
            profile.Name,
            records.Count,
            stopwatch.ElapsedMilliseconds,
            (cpuAfter - cpuBefore).TotalMilliseconds,
            (memAfter - memBefore) / (1024.0 * 1024.0));

        return Task.FromResult(records.Count);
    }

    private void WriteJsonOutput(
        ExtractionProfile profile,
        IReadOnlyList<Dictionary<string, object?>> records,
        int batchSize)
    {
        var filePath = profile.Output?.Json?.FilePath ?? profile.Output?.FilePath;
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        filePath = ExpandPlaceholders(filePath, profile.Mode, batchSize);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var pretty = profile.Output?.Json?.Pretty ?? profile.Output?.Pretty ?? false;
        var options = new JsonSerializerOptions
        {
            WriteIndented = pretty,
            // Do not escape non-ASCII characters (Cyrillic) to \uXXXX — write them as-is.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // DBNull (used for null DB values) must serialize as JSON null, not "{}".
            Converters = { new DBNullJsonConverter() },
        };

        var json = JsonSerializer.Serialize(records, options);
        File.WriteAllText(filePath, json);

        _logger.LogInformation("Written {Count} records to {FilePath}.", records.Count, filePath);
    }

    private static string ExpandPlaceholders(string path, string mode, int batchSize)
    {
        var now = DateTime.Now;
        var date = now.ToString("yyyy-MM-dd");
        var timestamp = now.ToString("yyyyMMdd-HHmmss");
        var batch = batchSize.ToString();

        // Support both {placeholder} and <placeholder> syntax.
        return path
            .Replace("{date}", date, StringComparison.Ordinal)
            .Replace("<date>", date, StringComparison.Ordinal)
            .Replace("{mode}", mode, StringComparison.Ordinal)
            .Replace("<mode>", mode, StringComparison.Ordinal)
            .Replace("{batch-size}", batch, StringComparison.Ordinal)
            .Replace("<batch-size>", batch, StringComparison.Ordinal)
            .Replace("{timestamp}", timestamp, StringComparison.Ordinal)
            .Replace("<timestamp>", timestamp, StringComparison.Ordinal);
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