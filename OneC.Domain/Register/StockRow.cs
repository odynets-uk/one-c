namespace OneC.Domain.Register;

/// <summary>
///     Represents a single stock row grouped by item GUID.
/// </summary>
public sealed record StockRow(
    string WarehouseGuid,
    string WarehouseName,
    decimal Quantity);