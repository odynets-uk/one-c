using System.Text.Json;
using OneC.Domain.Profiles;

namespace OneC.Infrastructure.Profiles;

/// <summary>
///     Loads and validates extraction profiles from JSON files.
/// </summary>
public static class ProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    ///     Loads a profile from a JSON file.
    /// </summary>
    /// <param name="profilePath">Path to the profile JSON file.</param>
    /// <returns>Loaded profile.</returns>
    /// <exception cref="FileNotFoundException">When the profile file does not exist.</exception>
    /// <exception cref="JsonException">When the profile JSON is invalid.</exception>
    public static ExtractionProfile Load(string profilePath)
    {
        if (!File.Exists(profilePath))
        {
            throw new FileNotFoundException($"Profile file not found: {profilePath}", profilePath);
        }

        var json = File.ReadAllText(profilePath);
        return Parse(json, Path.GetFileNameWithoutExtension(profilePath));
    }

    /// <summary>
    ///     Parses a profile from JSON string.
    /// </summary>
    /// <param name="json">JSON content.</param>
    /// <param name="profileName">Profile name (used when not present in JSON).</param>
    /// <returns>Loaded profile.</returns>
    /// <exception cref="JsonException">When the profile JSON is invalid.</exception>
    public static ExtractionProfile Parse(string json, string profileName)
    {
        var profile = JsonSerializer.Deserialize<ExtractionProfile>(json, JsonOptions)
                      ?? throw new JsonException("Profile JSON is empty.");

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            profile = profile with { Name = profileName };
        }

        if (string.IsNullOrWhiteSpace(profile.RootType))
        {
            throw new JsonException("Profile must specify 'rootType'.");
        }

        return profile;
    }
}