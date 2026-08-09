namespace OneC.Domain.Profiles;

/// <summary>
///     Fields to include in the output (legacy products-style profiles).
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