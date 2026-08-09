using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneC.Domain.ValueObjects;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Reads price and stock data from 1C registers via COM.
///     Mirrors the logic of the legacy Python extractor (getprices_com.md):
///     - Price types from Справочник.ТипыЦенНоменклатуры
///     - Latest price slice from РегистрСведений.ЦеныНоменклатуры.СрезПоследних
///     - Stock remainders from РегистрНакопления.ТоварыНаСкладах.Остатки
///     - Last movement from РегістрНакопления.ТоварыНаСкладах (GROUP BY)
///     Registers are filtered via a 1C array of references (В (&ItemsArray)).
///     References are built once (BuildRefCache) and reused across all loads.
/// </summary>
public sealed class RegisterDataReader
{
    // Batch size for the 1C array of references (В (&ItemsArray)).
    // Larger batches = fewer register passes. 50_000 covers the full catalog
    // in a single pass, avoiding repeated scans of the price register.
    private const int RefBatchSize = 50000;

    private readonly ComSession _session;
    private readonly ILogger _logger;

    // Cache COM ref object -> GUID string. Resolves each unique reference once,
    // avoiding repeated (very expensive) УникальныйИдентификатор COM round-trips
    // for the same item/price-type appearing in many rows or across batches.
    private readonly ConditionalWeakTable<object, string?> _guidCache = new();
    private readonly ConditionalWeakTable<object, string> _nameCache = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="RegisterDataReader" /> class.
    /// </summary>
    /// <param name="session">An established COM session.</param>
    /// <param name="logger">Logger instance.</param>
    public RegisterDataReader(ComSession session, ILogger logger)
    {
        _session = session;
        _logger = logger;
    }

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
    ///     Represents a single stock row grouped by item GUID.
    ///     The WarehouseGuid is nullable for rows where the reference could not be extracted.
    /// </summary>
    public sealed record StockRow(
        string WarehouseGuid,
        string WarehouseName,
        decimal Quantity);

    /// <summary>
    ///     Loads all price types from the catalog.
    /// </summary>
    public IReadOnlyList<PriceTypeInfo> LoadPriceTypes()
    {
        var sw = Stopwatch.StartNew();
        var result = new List<PriceTypeInfo>();
        dynamic query = _session.Connection.NewObject("Query");
        query.Text = """
                     SELECT
                         ТипыЦен.Ссылка AS Ref,
                         ТипыЦен.Description AS Name,
                         ТипыЦен.БазовыйТипЦен AS BaseType,
                         ТипыЦен.Рассчитывается AS IsCalculated,
                         ТипыЦен.ПроцентСкидкиНаценки AS MarkupPercent
                     FROM
                         Справочник.ТипыЦенНоменклатуры AS ТипыЦен
                     WHERE
                         NOT ТипыЦен.DeletionMark
                     """;

        dynamic table = query.Execute().Unload();
        int rowCount = table.Count();
        for (var i = 0; i < rowCount; i++)
        {
            dynamic row = table.Get(i);
            var guid = GetRefGuid(row.Ref);
            if (guid is null)
            {
                continue;
            }

            var baseGuid = GetRefGuid(row.BaseType);
            result.Add(new PriceTypeInfo(
                guid,
                row.Name?.ToString() ?? string.Empty,
                baseGuid,
                row.IsCalculated == true,
                ToDecimal(row.MarkupPercent)));
        }

        _logger.LogInformation("Loaded {Count} price types in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    ///     Builds a cache of GUID -> COM reference (1C catalog item Ref objects).
    ///     The refs are created once and reused by LoadPrices/LoadStock/LoadLastMovements
    ///     — avoids building identical reference arrays three times.
    /// </summary>
    /// <summary>
    ///     Cache of GUID -> COM reference, plus a reverse map IUnknown pointer -> GUID.
    ///     The reverse map lets us resolve item/price-type refs returned by queries
    ///     in O(1) WITHOUT a COM round-trip (УникальныйИдентификатор call per row),
    ///     which was the main bottleneck on the full base (~40k COM calls = ~250s).
    /// </summary>
    public sealed class RefCache
    {
        public required IReadOnlyDictionary<string, object> ByGuid { get; init; }
    }

    public RefCache BuildRefCache(
        IReadOnlyCollection<string> itemGuids,
        string catalogName)
    {
        var sw = Stopwatch.StartNew();
        var byGuid = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        dynamic catalogs = _session.Connection.Catalogs;
        var catalogsType = ((object)catalogs).GetType();
        dynamic catalog = catalogsType.InvokeMember(
            catalogName,
            BindingFlags.GetProperty,
            null,
            catalogs,
            null);

        foreach (var guid in itemGuids)
        {
            dynamic v8Guid = _session.Connection.NewObject("УникальныйИдентификатор", guid);
            dynamic refObj = catalog.GetRef(v8Guid);
            byGuid[guid] = (object)refObj;
        }

        _logger.LogInformation("Built {Count} reference cache entries in {ElapsedMs} ms.", byGuid.Count, sw.ElapsedMilliseconds);
        return new RefCache { ByGuid = byGuid };
    }

    public Dictionary<string, Dictionary<string, (decimal Price, decimal MarkupPct, string Unit, string Period)>> LoadPrices(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, Dictionary<string, (decimal, decimal, string, string)>>(StringComparer.OrdinalIgnoreCase);

        // Load the ENTIRE latest price slice (СрезПоследних without a В-filter).
        // The В (&ItemsArray) filter with 13k+ refs is what made 1C take ~250s.
        // Without the filter this is ONE fast query (like stock ~4s); matching to
        // the requested items happens in .NET via GUID. CatalogReader only builds
        // prices for the items it read, so extra rows are simply ignored.
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
        Dictionary<string, Dictionary<string, (decimal, decimal, string, string)>> result)
    {
        int rowCount = table.Count();
        for (var i = 0; i < rowCount; i++)
        {
            dynamic row = table.Get(i);
            var itemGuid = GetRefGuid(row.Item, refCache);
            var typeGuid = GetRefGuid(row.PriceType, refCache);
            if (itemGuid is null || typeGuid is null)
            {
                continue;
            }

            // Explicitly typed variables: row.X is dynamic, so ToDecimal
            // results would otherwise be typed dynamic → tuple literal becomes
            // ValueTuple<object,...> which cannot be stored in ValueTuple<decimal,decimal,string,string>.
            string unit = GetRefName(row.Unit);
            decimal price = ToDecimal(row.Price);
            decimal markupPct = ToDecimal(row.MarkupPct);
            string period = FormatDateTime(row.Period);
            var entry = new ValueTuple<decimal, decimal, string, string>(price, markupPct, unit, period);

            if (!result.TryGetValue(itemGuid, out Dictionary<string, (decimal, decimal, string, string)>? types))
            {
                types = new Dictionary<string, (decimal, decimal, string, string)>(StringComparer.OrdinalIgnoreCase);
                result[itemGuid] = types;
            }

            // Keep the latest record for this (item, type).
            if (!types!.TryGetValue(typeGuid, out (decimal, decimal, string, string) existing)
                || string.CompareOrdinal(entry.Item4, existing.Item4) > 0)
            {
                types[typeGuid] = entry;
            }
        }
    }

    /// <summary>
    ///     Loads current warehouse remainders (non-zero), grouped by item GUID.
    ///     Filters by a 1C array of references (В (&ItemsArray)).
    ///     Returns: itemGuid -> list of (warehouseGuid, warehouseName, quantity).
    /// </summary>
    public Dictionary<string, List<StockRow>> LoadStock(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, List<StockRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in itemGuids.Chunk(RefBatchSize))
        {
            dynamic query = _session.Connection.NewObject("Query");
            query.Text = """
                         SELECT
                             Остатки.Номенклатура AS Item,
                             Остатки.Склад AS Warehouse,
                             Остатки.КоличествоОстаток AS Quantity
                         FROM
                             РегистрНакопления.ТоварыНаСкладах.Остатки() AS Остатки
                         WHERE
                             Остатки.КоличествоОстаток <> 0
                             AND Остатки.Номенклатура В (&ItemsArray)
                         """;

            query.SetParameter("ItemsArray", CreateRefArray(batch, refCache));

            dynamic table = query.Execute().Unload();
            int rowCount = table.Count();
            for (var i = 0; i < rowCount; i++)
            {
                dynamic row = table.Get(i);
                var itemGuid = GetRefGuid(row.Item, refCache);
                var whGuid = GetRefGuid(row.Warehouse);
                if (itemGuid is null || whGuid is null)
                {
                    continue;
                }

                if (!result.TryGetValue(itemGuid, out List<StockRow>? rows))
                {
                    rows = new List<StockRow>();
                    result[itemGuid] = rows;
                }

                rows!.Add(new StockRow(whGuid, GetRefName(row.Warehouse), ToDecimal(row.Quantity)));
            }
        }

        _logger.LogInformation("Loaded stock for {Count} items in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    ///     Loads the last movement date per item+warehouse.
    ///     Filters by a 1C array of references (В (&ItemsArray)).
    ///     Returns a dictionary keyed by "itemGuid_warehouseGuid".
    /// </summary>
    public Dictionary<string, string> LoadLastMovements(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in itemGuids.Chunk(RefBatchSize))
        {
            dynamic query = _session.Connection.NewObject("Query");
            query.Text = """
                         SELECT
                             Рух.Номенклатура AS Item,
                             Рух.Склад AS Warehouse,
                             MAX(Рух.Период) AS LastMovement
                         FROM
                             РегистрНакопления.ТоварыНаСкладах AS Рух
                         WHERE
                             Рух.Номенклатура В (&ItemsArray)
                         GROUP BY
                             Рух.Номенклатура,
                             Рух.Склад
                         """;

            query.SetParameter("ItemsArray", CreateRefArray(batch, refCache));

            dynamic table = query.Execute().Unload();
            int rowCount = table.Count();
            for (var i = 0; i < rowCount; i++)
            {
                dynamic row = table.Get(i);
                var itemGuid = GetRefGuid(row.Item, refCache);
                var whGuid = GetRefGuid(row.Warehouse);
                if (itemGuid is null || whGuid is null)
                {
                    continue;
                }

                result[itemGuid + "_" + whGuid] = FormatDateTime(row.LastMovement);
            }
        }

        _logger.LogInformation("Loaded {Count} last movement rows in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>
    ///     Builds the prices list for a given item GUID, replicating export1.py:
    ///     - For "Цена закупочная": price = base_cost (the raw Цена value).
    ///     - For other (calculated) types: base_cost = 0, price = cost * (1 + markup_pct/100),
    ///       where cost = the latest "Цена закупочная" for this item.
    ///     base_cost returned is the latest known cost price for the item (0 for calculated types).
    /// </summary>
    private const string CostPriceTypeName = "Цена закупочная";

    public List<PriceEntry> BuildPrices(
        string itemGuid,
        IReadOnlyDictionary<string, PriceTypeInfo> priceTypesByGuid,
        Dictionary<string, Dictionary<string, (decimal Price, decimal MarkupPct, string Unit, string Period)>> pricesByItem)
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

    /// <summary>
    ///     Builds the stock list for a given item GUID using a pre-grouped dictionary.
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

    private string? GetRefGuid(object? refObject, RefCache? refCache = null)
    {
        if (refObject is null || refObject is DBNull)
        {
            return null;
        }

        if (!Marshal.IsComObject(refObject))
        {
            return null;
        }

        // Cache per RCW object. The Com interop layer reuses the same RCW
        // for the same underlying COM object when the same row/ValueTable value
        // is accessed repeatedly, so each unique reference resolves its GUID
        // exactly once — saving repeated, expensive УникальныйИдентификатор calls.
        if (_guidCache.TryGetValue(refObject, out var cached))
        {
            return cached;
        }

        // Extract the GUID via УникальныйИдентификатор (like CatalogReader.GetRefId).
        // String(ref) in 1C returns the display name, not the GUID.
        string? result = null;
        try
        {
            var type = refObject.GetType();
            var guid = type.InvokeMember(
                "УникальныйИдентификатор",
                BindingFlags.InvokeMethod,
                null,
                refObject,
                null);
            if (guid is not null)
            {
                result = _session.String(guid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("УникальныйИдентификатор failed: {Message}", ex.Message);
        }

        _guidCache.Add(refObject, result);
        return result;
    }

    private string GetRefName(object? refObject)
    {
        if (refObject is null || refObject is DBNull)
        {
            return string.Empty;
        }

        if (!Marshal.IsComObject(refObject))
        {
            return refObject.ToString() ?? string.Empty;
        }

        if (_nameCache.TryGetValue(refObject, out var cached))
        {
            return cached;
        }

        string name;
        try
        {
            var type = refObject.GetType();
            name = type.InvokeMember("Description", BindingFlags.GetProperty, null, refObject, null)?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            name = string.Empty;
        }

        _nameCache.Add(refObject, name);
        return name;
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