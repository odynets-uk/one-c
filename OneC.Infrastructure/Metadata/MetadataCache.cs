using System.Text.Json;
using OneC.Domain.Metadata;

namespace OneC.Infrastructure.Metadata;

/// <summary>
///     Caches the parsed metadata model to a JSON file for fast subsequent loads.
/// </summary>
public static class MetadataCache
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    ///     Loads metadata from cache if available, otherwise parses the XSD and caches it.
    /// </summary>
    /// <param name="xsdPath">Path to the XSD file.</param>
    /// <param name="cachePath">Path to the cache JSON file.</param>
    /// <returns>Parsed metadata model.</returns>
    public static MetadataModel LoadOrParse(string xsdPath, string cachePath)
    {
        if (File.Exists(cachePath))
        {
            try
            {
                var json = File.ReadAllText(cachePath);
                var cached = JsonSerializer.Deserialize<MetadataModel>(json, JsonOptions);
                if (cached is not null)
                {
                    return cached;
                }
            }
            catch (JsonException)
            {
                // Cache is corrupted — re-parse.
            }
        }

        var model = XsdMetadataParser.Parse(xsdPath);
        Save(model, cachePath);
        return model;
    }

    /// <summary>
    ///     Saves the metadata model to a JSON cache file.
    /// </summary>
    /// <param name="model">Metadata model.</param>
    /// <param name="cachePath">Path to the cache JSON file.</param>
    public static void Save(MetadataModel model, string cachePath)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(model, JsonOptions);
        File.WriteAllText(cachePath, json);
    }
}