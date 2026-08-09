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
    // Smaller batches = faster per-query execution in 1C. Larger arrays (13k+ refs)
    // made 1C very slow (~250s). 2000 refs per batch keeps each query fast.
    private const int RefBatchSize = 2000;

    public PriceLoader(IComSession session, ReferenceResolver referenceResolver, ILogger logger)
    {
        _session = session;
        _referenceResolver = referenceResolver;
        _logger = logger;
    }

    /// <summary>
    ///     Loads the latest price slice (СрезПоследних) for the given items.
    ///     Filters by a 1C array of references (В (&ItemsArray)) — only loads prices
    ///     for the requested items instead of the entire price register.
    ///     Batching avoids passing the full catalog in a single array (13k+ refs
    ///     made 1C take ~250s per pass).
    /// </summary>
    public Dictionary<string, Dictionary<string, PriceRow>> LoadPrices(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, Dictionary<string, PriceRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in itemGuids.Chunk(RefBatchSize))
        {
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
                         WHERE
                             Цены.Номенклатура В (&ItemsArray)
                         """;

            query.SetParameter("ItemsArray", CreateRefArray(batch, refCache));

            dynamic table = query.Execute().Unload();
            ProcessPriceTable(table, refCache, result);
        }

        _logger.LogInformation("Loaded prices for {Count} items in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    ///     Creates a 1C Массив of catalog references for the given GUID batch,
    ///     reusing the already-built reference cache.
    /// </summary>
    private dynamic CreateRefArray(IEnumerable<string> guids, RefCache refCache)
    {
        dynamic v8Array = _session.Connection.NewObject("Массив");
        foreach (var guid in guids)
        {
            v8Array.Add(refCache.ByGuid[guid]);
        }

        return v8Array;
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