namespace OneC.Domain.Profiles;

/// <summary>
///     Represents an extraction profile that describes how to read data from 1C.
///     Concrete profiles (catalog, products, expenses) are defined as JSON files.
/// </summary>
public sealed record ExtractionProfile
{
    /// <summary>
    ///     Gets the profile name (e.g. "products"). Filled in by the loader if absent.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    ///     Gets the root 1C type to read (e.g. "CatalogObject.Номенклатура").
    /// </summary>
    public required string RootType { get; init; }

    /// <summary>
    ///     Gets the output settings (file path, pretty print).
    /// </summary>
    public OutputSettings? Output { get; init; }

    /// <summary>
    ///     Gets the filters (prices, stock, items).
    /// </summary>
    public Filters? Filters { get; init; }

    /// <summary>
    ///     Gets the fields to include in the output.
    /// </summary>
    public IncludeFields? IncludeFields { get; init; }

    /// <summary>
    ///     Gets a value indicating whether to skip items without prices.
    /// </summary>
    public bool SkipItemsWithoutPrices { get; init; }

    /// <summary>
    ///     Gets a value indicating whether to skip items without stock.
    /// </summary>
    public bool SkipItemsWithoutStock { get; init; }
}