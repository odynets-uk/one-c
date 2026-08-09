namespace OneC.Domain.Register;

/// <summary>
///     Represents a price type definition from the catalog.
/// </summary>
public sealed record PriceTypeInfo(
    string Guid,
    string Name,
    string? BaseTypeGuid,
    bool IsCalculated,
    decimal MarkupPercent);

/// <summary>
///     Represents a single price entry for an item.
/// </summary>
public sealed record PriceEntry(
    string PriceType,
    decimal Price,
    decimal BaseCost,
    decimal MarkupPct,
    string Unit,
    string Timestamp);

/// <summary>
///     Represents a single stock entry for an item.
/// </summary>
public sealed record StockEntry(
    string Warehouse,
    int StatusCode,
    decimal Quantity,
    string LastMovement);

/// <summary>
///     Cache of GUID -> COM reference, plus a reverse map IUnknown pointer -> GUID.
///     The reverse map lets us resolve item/price-type refs returned by queries
///     in O(1) WITHOUT a COM round-trip (УникальныйИдентификатор call per row),
///     which was the main bottleneck on the full base (~40k COM calls = ~250s).
/// </summary>
public sealed class RefCache
{
    public required IReadOnlyDictionary<string, object> ByGuid { get; init; }
    public required Dictionary<IntPtr, string> ByIUnknown { get; init; }
}