using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OneC.Domain.Register;

/// <summary>
///     Builds the prices list for a given item GUID, replicating export1.py logic:
///     - For "Цена закупочная": price = base_cost (the raw Цена value).
///     - For other (calculated) types: base_cost = 0, price = cost * (1 + markup_pct/100),
///       where cost = the latest "Цена закупочная" for this item.
///     base_cost returned is the latest known cost price for the item (0 for calculated types).
/// </summary>
public sealed class PriceCalculator
{
    private readonly ILogger _logger;

    /// <summary>
    ///     The name of the cost price type in 1C (used as base for calculated prices).
    /// </summary>
    public const string CostPriceTypeName = "Цена закупочная";

    public PriceCalculator(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Builds the prices list for a given item GUID.
    /// </summary>
    public List<PriceEntry> BuildPrices(
        string itemGuid,
        IReadOnlyDictionary<string, PriceTypeInfo> priceTypesByGuid,
        Dictionary<string, Dictionary<string, PriceRow>> pricesByItem)
    {
        var sw = Stopwatch.StartNew();
        var result = new List<PriceEntry>();
        if (!pricesByItem.TryGetValue(itemGuid, out var itemPrices))
        {
            return result;
        }

        // Latest known cost price for this item (used as base for calculated types).
        decimal cost = 0m;
        foreach (var type in priceTypesByGuid.Values)
        {
            if (!type.Name.Equals(CostPriceTypeName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (itemPrices.TryGetValue(type.Guid, out var costRow))
            {
                cost = costRow.Price;
            }
            break;
        }

        foreach (var type in priceTypesByGuid.Values)
        {
            if (!itemPrices.TryGetValue(type.Guid, out var priceRow))
            {
                continue;
            }

            if (type.Name.Equals(CostPriceTypeName, StringComparison.OrdinalIgnoreCase))
            {
                // Закупочная: real price = base_cost.
                result.Add(new PriceEntry(
                    type.Name,
                    priceRow.Price,
                    priceRow.Price, // base_cost
                    0m, // markup_pct
                    priceRow.Unit,
                    priceRow.Period));
            }
            else
            {
                // Calculated type: price = cost * (1 + markup_pct/100), base_cost = 0.
                if (cost == 0m)
                {
                    continue;
                }

                var realPrice = Math.Round(cost * (1 + priceRow.MarkupPct / 100m), 2);
                result.Add(new PriceEntry(
                    type.Name,
                    realPrice,
                    0m,
                    priceRow.MarkupPct,
                    priceRow.Unit,
                    priceRow.Period));
            }
        }

        _logger.LogDebug("Built {Count} prices for item {ItemGuid} in {ElapsedMs} ms.", result.Count, itemGuid, sw.ElapsedMilliseconds);
        return result;
    }
}