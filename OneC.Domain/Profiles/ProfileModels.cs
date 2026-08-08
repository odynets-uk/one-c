namespace OneC.Domain.Profiles;

/// <summary>
///     Output settings for an extraction profile.
/// </summary>
public sealed record OutputSettings
{
    /// <summary>
    ///     Gets the output file path (may contain {date} placeholder).
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    ///     Gets a value indicating whether to pretty-print JSON.
    /// </summary>
    public bool Pretty { get; init; }
}

/// <summary>
///     Filters for an extraction profile.
/// </summary>
public sealed record Filters
{
    /// <summary>
    ///     Gets the price filters.
    /// </summary>
    public PriceFilter? Prices { get; init; }

    /// <summary>
    ///     Gets the stock filters.
    /// </summary>
    public StockFilter? Stock { get; init; }

    /// <summary>
    ///     Gets the item filters.
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
///     Fields to include in the output.
/// </summary>
public sealed record IncludeFields
{
    /// <summary>
    ///     Gets the item fields to include.
    /// </summary>
    public IReadOnlyList<string> Item { get; init; } = [];

    /// <summary>
    ///     Gets the price fields to include.
    /// </summary>
    public IReadOnlyList<string> Price { get; init; } = [];

    /// <summary>
    ///     Gets the stock fields to include.
    /// </summary>
    public IReadOnlyList<string> Stock { get; init; } = [];
}