namespace OneC.Domain.Register;

/// <summary>
///     Represents a single price row from the price register (СрезПоследних).
/// </summary>
public sealed record PriceRow(
    decimal Price,
    decimal MarkupPct,
    string Unit,
    string Period);