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
    private readonly RefArrayFactory _refArrayFactory;
    private readonly ILogger _logger;

    // Batch size for the 1C array of references (В (&ItemsArray)).
    // Smaller batches = faster per-query execution in 1C. Larger arrays (13k+ refs)
    // made 1C very slow (~250s). 2000 refs per batch keeps each query fast.
    private const int RefBatchSize = 2000;

    public PriceLoader(IComSession session, ReferenceResolver referenceResolver, RefArrayFactory refArrayFactory, ILogger logger)
    {
        _session = session;
        _referenceResolver = referenceResolver;
        _refArrayFactory = refArrayFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Loads the latest price slice (СрезПоследних) for the given items.
    ///     Filters by a 1C array of references (В (&ItemsArray)) — only loads prices
    ///     for the requested items instead of the entire price register.
    ///     Batching avoids passing the full catalog in a single array (13k+ refs
    ///     made 1C take ~250s per pass).
    ///     When <paramref name="changedSince" /> is provided, reads the physical register
    ///     table with a period filter — only prices that changed within the period are
    ///     returned (latest per item+type).
    /// </summary>
    public Dictionary<string, Dictionary<string, PriceRow>> LoadPrices(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache,
        string? changedSince = null)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, Dictionary<string, PriceRow>>(StringComparer.OrdinalIgnoreCase);
        var (sinceDate, toDate) = PeriodParser.Parse(changedSince);

        foreach (var batch in itemGuids.Chunk(RefBatchSize))
        {
            dynamic query = _session.Connection.NewObject("Query");

            // With changed_since: read the PHYSICAL register table and filter by period.
            // The physical table has one row per change; ProcessPriceTable below keeps
            // the latest record per (item, type) — i.e. the latest price within the window.
            if (sinceDate is not null)
            {
                var text = """
                           SELECT
                               Цены.Номенклатура AS Item,
                               Цены.ТипЦен AS PriceType,
                               Цены.Цена AS Price,
                               Цены.ПроцентСкидкиНаценки AS MarkupPct,
                               Цены.ЕдиницаИзмерения AS Unit,
                               Цены.Период AS Period
                           FROM
                               РегистрСведений.ЦеныНоменклатуры AS Цены
                           WHERE
                               Цены.Номенклатура В (&ItemsArray)
                               AND Цены.Период >= &SinceDate
                           """;

                if (toDate is not null)
                {
                    text = text.Replace("AND Цены.Период >= &SinceDate", "AND Цены.Период >= &SinceDate AND Цены.Период <= &ToDate");
                }

                query.Text = text;
                query.SetParameter("ItemsArray", _refArrayFactory.CreateRefArray(batch, refCache));
                query.SetParameter("SinceDate", sinceDate.Value);
                if (toDate is not null)
                {
                    query.SetParameter("ToDate", toDate.Value);
                }
            }
            else
            {
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

                query.SetParameter("ItemsArray", _refArrayFactory.CreateRefArray(batch, refCache));
            }

            dynamic table = query.Execute().Unload();
            ProcessPriceTable(table, refCache, result);
        }

        _logger.LogInformation("Loaded prices for {Count} items in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    ///     Returns the distinct item GUIDs that had a price change within the given
    ///     period. Reads the PHYSICAL register table (one row per change) with a
    ///     period filter — no item filter, because the changed GUIDs are unknown yet.
    ///     Used to pre-filter the catalog before reading it.
    /// </summary>
    public IReadOnlyCollection<string> LoadChangedItemGuids(string changedSince)
    {
        var sw = Stopwatch.StartNew();
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var (sinceDate, toDate) = PeriodParser.Parse(changedSince);
        if (sinceDate is null)
        {
            return result;
        }

        dynamic query = _session.Connection.NewObject("Query");

        var text = """
                   SELECT DISTINCT
                       Цены.Номенклатура AS Item
                   FROM
                       РегистрСведений.ЦеныНоменклатуры AS Цены
                   WHERE
                       Цены.Период >= &SinceDate
                   """;

        if (toDate is not null)
        {
            text = text.Replace("Цены.Период >= &SinceDate", "Цены.Период >= &SinceDate AND Цены.Период <= &ToDate");
        }

        query.Text = text;
        query.SetParameter("SinceDate", sinceDate.Value);
        if (toDate is not null)
        {
            query.SetParameter("ToDate", toDate.Value);
        }

        dynamic table = query.Execute().Unload();
        int rowCount = table.Count();
        for (var i = 0; i < rowCount; i++)
        {
            dynamic row = table.Get(i);
            var itemGuid = _referenceResolver.GetRefGuid(row.Item);
            if (itemGuid is not null)
            {
                result.Add(itemGuid);
            }
        }

        _logger.LogInformation("Loaded {Count} changed item GUIDs (prices) in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
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