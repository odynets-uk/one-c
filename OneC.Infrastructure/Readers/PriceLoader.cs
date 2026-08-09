using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Loads prices from the 1C price register (РегистрСведений.ЦеныНоменклатуры.СрезПоследних).
/// </summary>
public sealed class PriceLoader
{
    private readonly IComSession _session;
    private readonly ReferenceResolver _referenceResolver;
    private readonly ILogger _logger;

    // Batch size for the 1C array of references (В (&ItemsArray)).
    // Larger batches = fewer register passes. 50_000 covers the full catalog
    // in a single pass, avoiding repeated scans of the price register.
    private const int RefBatchSize = 50000;

    public PriceLoader(IComSession session, ReferenceResolver referenceResolver, ILogger logger)
    {
        _session = session;
        _referenceResolver = referenceResolver;
        _logger = logger;
    }

    /// <summary>
    ///     Loads the latest price slice (СрезПоследних) for the given items.
    ///     Loads the ENTIRE latest price slice (without a В-filter) for performance.
    ///     The В (&ItemsArray) filter with 13k+ refs is what made 1C take ~250s.
    ///     Without the filter this is ONE fast query (like stock ~4s); matching to
    ///     the requested items happens in .NET via GUID.
    /// </summary>
    public Dictionary<string, Dictionary<string, PriceRow>> LoadPrices(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, Dictionary<string, PriceRow>>(StringComparer.OrdinalIgnoreCase);

        dynamic query = _session.Connection.NewObject("Query");
        query.Text = """
                     SELECT
                         Цены.Номенклатура AS Item,
                         Цены.ТипЦен AS PriceType,
                         Цены.Цена AS Price,
                         Цены.ПроцентСкидкиНаценки AS MarkupPct,
                         Цены.ЕдиницаИзмерения AS Unit,
                         Цены.Период AS Period
                     FROM
                         РегистрСведений.ЦеныНоменклатуры.СрезПоследних() AS Цены
                     """;

        dynamic table = query.Execute().Unload();
        ProcessPriceTable(table, refCache, result);

        _logger.LogInformation("Loaded prices for {Count} items in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    ///     Reads the price result table into the result dictionary,
    ///     keeping the latest record per (item, type). Resolves item/price-type
    ///     GUIDs via the per-RCW cache — a GUID is resolved once per unique reference.
    /// </summary>
    private void ProcessPriceTable(
        dynamic table,
        RefCache refCache,
        Dictionary<string, Dictionary<string, PriceRow>> result)
    {
        int rowCount = table.Count();
        for (var i = 0; i < rowCount; i++)
        {
            dynamic row = table.Get(i);
            var itemGuid = _referenceResolver.GetRefGuid(row.Item, refCache);
            var typeGuid = _referenceResolver.GetRefGuid(row.PriceType, refCache);
            if (itemGuid is null || typeGuid is null)
            {
                continue;
            }

            string unit = _referenceResolver.GetRefName(row.Unit);
            decimal price = ToDecimal(row.Price);
            decimal markupPct = ToDecimal(row.MarkupPct);
            string period = FormatDateTime(row.Period);
            var entry = new PriceRow(price, markupPct, unit, period);

            if (!result.TryGetValue(itemGuid, out Dictionary<string, PriceRow>? types))
            {
                types = new Dictionary<string, PriceRow>(StringComparer.OrdinalIgnoreCase);
                result[itemGuid] = types;
            }

            // Keep the latest record for this (item, type).
            if (!types!.TryGetValue(typeGuid, out PriceRow existing)
                || string.CompareOrdinal(entry.Period, existing.Period) > 0)
            {
                types[typeGuid] = entry;
            }
        }
    }

    private static decimal ToDecimal(object? value)
    {
        if (value is null || value is DBNull)
        {
            return 0m;
        }

        try
        {
            return Convert.ToDecimal(value);
        }
        catch (Exception)
        {
            return 0m;
        }
    }

    private static string FormatDateTime(object? value)
    {
        if (value is null || value is DBNull)
        {
            return string.Empty;
        }

        try
        {
            var dt = Convert.ToDateTime(value);
            return dt.ToString("yyyy-MM-dd HH:mm:ss");
        }
        catch (Exception)
        {
            return value.ToString() ?? string.Empty;
        }
    }
}