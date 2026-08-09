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

                var validation = column.Validation;
                if (validation is not null)
                {
                    // 'vo' must be a known domain Value Object.
                    if (!string.IsNullOrWhiteSpace(validation.Vo) &&
                        !string.Equals(validation.Vo, "OneCRef", StringComparison.Ordinal))
                    {
                        errors.Add($"Column '{column.Name}': unknown 'vo' '{validation.Vo}'. Supported: 'OneCRef'.");
                    }

                    // 'exists' must be in "table.column" format.
                    if (!string.IsNullOrWhiteSpace(validation.Exists) &&
                        !IsTableColumn(validation.Exists))
                    {
                        errors.Add(
                            $"Column '{column.Name}': 'exists' must be in 'table.column' format, got '{validation.Exists}'.");
                    }

                    // 'empty_to_null' only makes sense for JSON/JSONB columns.
                    if (validation.EmptyToNull &&
                        !column.SqlType.StartsWith("JSON", StringComparison.OrdinalIgnoreCase))
                    {
                        errors.Add(
                            $"Column '{column.Name}': 'empty_to_null' requires 'sql_type' JSON/JSONB, got '{column.SqlType}'.");
                    }
                }
            }

            // Ensure 'id' column (primary key) exists.
            if (profile.Columns.All(c => !c.Name.Equals("id", StringComparison.OrdinalIgnoreCase)))
            {
                errors.Add("Profile must define an 'id' column (primary key).");
            }
        }

        if (profile.Filters?.Prices is { } prices)
        {
            if (!string.IsNullOrWhiteSpace(prices.ChangedSince) && !IsValidPeriod(prices.ChangedSince))
            {
                errors.Add(
                    $"Profile 'filters.prices.changed_since' must be relative (e.g. '14d', '2w', '6h') or range 'YYYY-MM-DD:YYYY-MM-DD', got '{prices.ChangedSince}'.");
            }
        }

        if (profile.Filters?.Stock is { } stock)
        {
            if (!string.IsNullOrWhiteSpace(stock.ChangedSince) && !IsValidPeriod(stock.ChangedSince))
            {
                errors.Add(
                    $"Profile 'filters.stock.changed_since' must be relative (e.g. '14d', '2w', '6h') or range 'YYYY-MM-DD:YYYY-MM-DD', got '{stock.ChangedSince}'.");
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

    private static bool IsTableColumn(string value)
    {
        var parts = value.Split('.');
        return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]);
    }

    private static bool IsValidPeriod(string value)
    {
        // Relative: "14d", "2w", "6h", "30m", "45s"
        if (value.Length > 1 && int.TryParse(value[..^1], out _) && "smhdw".Contains(value[^1]))
        {
            return true;
        }

        // Absolute range: "2026-07-01:2026-07-31"
        var parts = value.Split(':');
        return parts.Length == 2 &&
               DateOnly.TryParse(parts[0], out _) &&
               DateOnly.TryParse(parts[1], out _);
    }
}
