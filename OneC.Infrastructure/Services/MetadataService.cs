using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OneC.Application.Abstractions.Services;
using OneC.Domain.Metadata;
using OneC.Infrastructure.Metadata;

namespace OneC.Infrastructure.Services;

/// <summary>
///     Provides 1C metadata from the XSD schema with JSON caching.
/// </summary>
public sealed class MetadataService : IMetadataService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MetadataService> _logger;
    private MetadataModel? _cached;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MetadataService" /> class.
    /// </summary>
    /// <param name="configuration">Configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public MetadataService(IConfiguration configuration, ILogger<MetadataService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<MetadataModel> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return Task.FromResult(_cached);
        }

        var xsdPath = ResolvePath(_configuration["Metadata:SchemaPath"] ?? "data-enterprise.xsd");
        var cachePath = ResolvePath(_configuration["Metadata:CachePath"] ?? "metadata-cache.json");

        if (!File.Exists(xsdPath))
        {
            var error = $"XSD schema file not found: {xsdPath}";
            _logger.LogError("{Error}", error);
            throw new FileNotFoundException(error, xsdPath);
        }

        _logger.LogInformation("Loading metadata from {XsdPath}...", xsdPath);
        var model = MetadataCache.LoadOrParse(xsdPath, cachePath);
        _logger.LogInformation(
            "Metadata loaded: {CatalogCount} catalogs, {EnumCount} enums.",
            model.Catalogs.Count,
            model.Enums.Count);

        _cached = model;
        return Task.FromResult(model);
    }

    private static string ResolvePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        // Prefer the file next to the executable (output directory).
        var basePath = Path.Combine(AppContext.BaseDirectory, path);
        if (File.Exists(basePath))
        {
            return basePath;
        }

        return path;
    }
}
