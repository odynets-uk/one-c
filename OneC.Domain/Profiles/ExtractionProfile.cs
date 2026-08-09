namespace OneC.Domain.Profiles;

/// <summary>
///     Represents an extraction profile that describes how to read data from 1C.
///     Concrete profiles (catalog, products, expenses) are defined as JSON files.
/// </summary>
public sealed record ExtractionProfile
{
    /// <summary>
    ///     Gets the profile name (e.g. "categories"). Filled in by the loader if absent.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Gets the source settings — where to read data from in 1C.
    /// </summary>
    public ProfileSource Source { get; init; } = new() { Type = string.Empty };

    /// <summary>
    ///     Gets the legacy root 1C type (backward compatibility, use Source.Type instead).
    /// </summary>
    public string? RootType { get; init; }

    /// <summary>
    ///     Gets the sync mode: "full" (default) or "incremental".
    /// </summary>
    public string Mode { get; init; } = "full";

    /// <summary>
    ///     Gets the batch size for reading. -1 = read all records without batching (default for catalogs).
    /// </summary>
    public int BatchSize { get; init; } = -1;

    /// <summary>
    ///     Gets the output settings (JSON file, SQLite DB).
    /// </summary>
    public ProfileOutput? Output { get; init; }

    /// <summary>
    ///     Gets the filters (field filters, prices, stock, items).
    /// </summary>
    public ProfileFilters? Filters { get; init; }

    /// <summary>
    ///     Gets the target SQLite table name.
    /// </summary>
    public string? Table { get; init; }

    /// <summary>
    ///     Gets the column mappings (1C field → SQLite column).
    /// </summary>
    public IReadOnlyList<ProfileColumn> Columns { get; init; } = [];

    /// <summary>
    ///     Gets the foreign key references.
    /// </summary>
    public IReadOnlyList<ProfileReference> References { get; init; } = [];

    /// <summary>
    ///     Gets the database indexes.
    /// </summary>
    public IReadOnlyList<ProfileIndex> Indexes { get; init; } = [];

    /// <summary>
    ///     Legacy: fields to include in the output (backward compatibility).
    /// </summary>
    public IncludeFields? IncludeFields { get; init; }

    /// <summary>
    ///     Legacy: skip items without prices (backward compatibility).
    /// </summary>
    public bool SkipItemsWithoutPrices { get; init; }

    /// <summary>
    ///     Legacy: skip items without stock (backward compatibility).
    /// </summary>
    public bool SkipItemsWithoutStock { get; init; }
}