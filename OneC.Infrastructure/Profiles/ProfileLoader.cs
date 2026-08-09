using System.Text.Json;
using OneC.Domain.Profiles;

namespace OneC.Infrastructure.Profiles;

/// <summary>
///     Loads, validates and logs errors for extraction profiles from JSON files.
/// </summary>
public static class ProfileLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
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

        Validate(profile);
        return profile;
    }

    /// <summary>
    ///     Validates the profile and throws <see cref="JsonException" /> with a clear message on failure.
    /// </summary>
    /// <param name="profile">Profile to validate.</param>
    /// <exception cref="JsonException">When the profile is invalid.</exception>
    public static void Validate(ExtractionProfile profile)
    {
        var errors = new List<string>();

        // Source type: either source.type or legacy rootType must be present.
        var sourceType = profile.Source?.Type;
        if (string.IsNullOrWhiteSpace(sourceType) && string.IsNullOrWhiteSpace(profile.RootType))
        {
            errors.Add("Profile must specify 'source.type' or legacy 'rootType'.");
        }

        if (string.IsNullOrWhiteSpace(profile.Table))
        {
            errors.Add("Profile must specify 'table'.");
        }

        if (profile.Columns.Count == 0)
        {
            errors.Add("Profile must specify at least one 'column'.");
        }

        if (profile.Columns.Count > 0)
        {
            // Validate each column.
            foreach (var column in profile.Columns)
            {
                if (string.IsNullOrWhiteSpace(column.Source))
                {
                    errors.Add($"Column '{column.Name}': 'source' is required.");
                }

                if (string.IsNullOrWhiteSpace(column.SqlType))
                {
                    errors.Add($"Column '{column.Name}': 'sql_type' is required.");
                }
            }

            // Ensure 'id' column (primary key) exists.
            if (profile.Columns.All(c => !c.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Profile must define an 'id' column (primary key).");
            }
        }

        if (profile.Mode is not ("full" or "incremental"))
        {
            errors.Add($"Profile 'mode' must be 'full' or 'incremental', got '{profile.Mode}'.");
        }

        if (errors.Count > 0)
        {
            throw new JsonException($"Profile validation failed:{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", errors)}");
        }
    }
}