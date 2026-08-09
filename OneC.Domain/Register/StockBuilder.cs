using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OneC.Domain.Register;

/// <summary>
///     Builds the stock list for a given item GUID using a pre-grouped dictionary.
/// </summary>
public sealed class StockBuilder
{
    private readonly ILogger _logger;

    public StockBuilder(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Builds the stock list for a given item GUID.
    /// </summary>
    public List<StockEntry> BuildStock(
        string itemGuid,
        Dictionary<string, List<StockRow>> stockByItem,
        Dictionary<string, string> lastMovements)
    {
        var sw = Stopwatch.StartNew();
        var result = new List<StockEntry>();
        if (!stockByItem.TryGetValue(itemGuid, out var rows))
        {
            return result;
        }

        foreach (var row in rows)
        {
            var key = itemGuid + "_" + row.WarehouseGuid;
            lastMovements.TryGetValue(key, out var lastMovement);

            result.Add(new StockEntry(
                row.WarehouseName,
                0, // status_code placeholder
                row.Quantity,
                lastMovement ?? string.Empty));
        }

        _logger.LogDebug("Built {Count} stock entries for item {ItemGuid} in {ElapsedMs} ms.", result.Count, itemGuid, sw.ElapsedMilliseconds);
        return result;
    }
}