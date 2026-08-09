namespace OneC.Domain.Profiles;

/// <summary>
///     Profile source settings — where to read data from in 1C.
/// </summary>
public sealed record ProfileSource
{
    /// <summary>
    ///     Gets the root 1C type to read (e.g. "CatalogObject.Номенклатура").
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    ///     Gets the XSD schema path (optional).
    /// </summary>
    public string? Schema { get; init; }
}

/// <summary>
///     Output settings for an extraction profile.
/// </summary>
public sealed record ProfileOutput
{
    /// <summary>
    ///     Gets the JSON output settings.
    /// </summary>
    public JsonOutputSettings? Json { get; init; }

    /// <summary>
    ///     Gets the database output settings.
    /// </summary>
    public DbOutputSettings? Db { get; init; }

    /// <summary>
    ///     Legacy: output file path (may contain {date} placeholder). Kept for backward compatibility.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    ///     Legacy: pretty-print flag. Kept for backward compatibility.
    /// </summary>
    public bool Pretty { get; init; }

    /// <summary>
    ///     Legacy: database path. Kept for backward compatibility.
    /// </summary>
    public string? DbPath { get; init; }
}

/// <summary>
///     JSON output settings.
/// </summary>
public sealed record JsonOutputSettings
{
    /// <summary>
    ///     Gets the output file path (may contain {date} placeholder).
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    ///     Gets a value indicating whether to pretty-print JSON.
    /// </summary>
    public bool Pretty { get; init; }
}

/// <summary>
///     Database output settings.
/// </summary>
public sealed record DbOutputSettings
{
    /// <summary>
    ///     Gets the database file path.
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    ///     Gets the database engine (e.g. "sqlite").
    /// </summary>
    public string? Engine { get; init; }

    /// <summary>
    ///     Gets the required database version (e.g. "3.37+").
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
///     Filters for an extraction profile.
/// </summary>
public sealed record ProfileFilters
{
    /// <summary>
    ///     Gets the field filters (e.g. { "IsFolder": true }).
    /// </summary>
    public IReadOnlyDictionary<string, object> FieldFilters { get; init; } =
        new Dictionary<string, object>();

    /// <summary>
    ///     Gets the price filters (for products profiles).
    /// </summary>
    public PriceFilter? Prices { get; init; }

    /// <summary>
    ///     Gets the stock filters (for products profiles).
    /// </summary>
    public StockFilter? Stock { get; init; }

    /// <summary>
    ///     Gets the item filters (for products profiles).
    /// </summary>
    public ItemFilter? Items { get; init; }
}

/// <summary>
///     Price filters.
/// </summary>
public sealed record PriceFilter
{
    /// <summary>
    ///     Gets the "changed since" date (null = all).
    /// </summary>
    public DateTime? ChangedSince { get; init; }

    /// <summary>
    ///     Gets the list of price types to include (empty = all).
    /// </summary>
    public IReadOnlyList<string> PriceTypes { get; init; } = [];

    /// <summary>
    ///     Gets a value indicating whether to exclude items with zero price.
    /// </summary>
    public bool ExcludeZeroPrice { get; init; }
}

/// <summary>
///     Stock filters.
/// </summary>
public sealed record StockFilter
{
    /// <summary>
    ///     Gets the "changed since" date (null = all).
    /// </summary>
    public DateTime? ChangedSince { get; init; }

    /// <summary>
    ///     Gets the list of warehouses to include (empty = all).
    /// </summary>
    public IReadOnlyList<string> Warehouses { get; init; } = [];

    /// <summary>
    ///     Gets the list of status codes to include (empty = all).
    /// </summary>
    public IReadOnlyList<string> StatusCodes { get; init; } = [];

    /// <summary>
    ///     Gets a value indicating whether to include only positive stock.
    /// </summary>
    public bool OnlyPositive { get; init; }
}

/// <summary>
///     Item filters.
/// </summary>
public sealed record ItemFilter
{
    /// <summary>
    ///     Gets the list of item codes to include (empty = all).
    /// </summary>
    public IReadOnlyList<string> Codes { get; init; } = [];

    /// <summary>
    ///     Gets the list of item artikuls to include (empty = all).
    /// </summary>
    public IReadOnlyList<string> Artikuls { get; init; } = [];

    /// <summary>
    ///     Gets the list of item GUIDs to include (empty = all).
    /// </summary>
    public IReadOnlyList<string> Guids { get; init; } = [];

    /// <summary>
    ///     Gets the name substring filter (empty = all).
    /// </summary>
    public string? NameContains { get; init; }
}

/// <summary>
///     Column mapping definition — describes how a 1C source field maps to a database column.
/// </summary>
public sealed record ProfileColumn
{
    /// <summary>
    ///     Gets the source 1C field name (e.g. "Ref", "DeletionMark").
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    ///     Gets the target column name (e.g. "id", "is_active").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the SQLite column definition (e.g. "TEXT PRIMARY KEY").
    /// </summary>
    public required string SqlType { get; init; }

    /// <summary>
    ///     Gets the value transformation expression (e.g. "NOT {value}" for DeletionMark → is_active).
    ///     "{value}" represents the raw source value.
    /// </summary>
    public string? Transform { get; init; }

    /// <summary>
    ///     Gets the validation rules for the value.
    /// </summary>
    public ColumnValidation? Validation { get; init; }
}

/// <summary>
///     Column validation rules.
/// </summary>
public sealed record ColumnValidation
{
    /// <summary>
    ///     Gets a value indicating whether the value is required.
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the value can be null.
    /// </summary>
    public bool Nullable { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the value must be unique.
    /// </summary>
    public bool Unique { get; init; }

    /// <summary>
    ///     Gets the regex pattern the value must match.
    /// </summary>
    public string? Regex { get; init; }

    /// <summary>
    ///     Gets the minimum string length.
    /// </summary>
    public int? MinLength { get; init; }

    /// <summary>
    ///     Gets the maximum string length.
    /// </summary>
    public int? MaxLength { get; init; }

    /// <summary>
    ///     Gets the case sensitivity for regex matching ("insensitive" or null).
    /// </summary>
    public string? Case { get; init; }

    /// <summary>
    ///     Gets the boolean validation mode ("strict" or null).
    /// </summary>
    public string? Boolean { get; init; }
}

/// <summary>
///     Foreign key reference definition.
/// </summary>
public sealed record ProfileReference
{
    /// <summary>
    ///     Gets the column that references another table.
    /// </summary>
    public required string Column { get; init; }

    /// <summary>
    ///     Gets the referenced table and column (e.g. "categories(id)").
    /// </summary>
    public required string References { get; init; }

    /// <summary>
    ///     Gets the ON DELETE behavior (RESTRICT, CASCADE, SET NULL, NO ACTION).
    /// </summary>
    public string? OnDelete { get; init; }

    /// <summary>
    ///     Gets the ON UPDATE behavior.
    /// </summary>
    public string? OnUpdate { get; init; }
}

/// <summary>
///     Index definition.
/// </summary>
public sealed record ProfileIndex
{
    /// <summary>
    ///     Gets the index name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Gets the columns included in the index.
    /// </summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>
    ///     Gets a value indicating whether the index is unique.
    /// </summary>
    public bool Unique { get; init; }

    /// <summary>
    ///     Gets the raw CREATE INDEX SQL (alternative to structured definition).
    /// </summary>
    public string? Sql { get; init; }
}