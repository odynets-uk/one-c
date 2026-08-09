using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Reads price and stock data from 1C registers via COM.
///     Mirrors the logic of the legacy Python extractor (getprices_com.md):
///     - Price types from Справочник.ТипыЦенНоменклатуры
///     - Latest price slice from РегистрСведений.ЦеныНоменклатуры.СрезПоследних
///     - Stock remainders from РегистрНакопления.ТоварыНаСкладах.Остатки
///     - Last movement from РегистрНакопления.ТоварыНаСкладах (GROUP BY)
/// </summary>
public sealed class RegisterDataReader
{
    private readonly ComSession _session;
    private readonly ILogger _logger;

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
    ///     Loads all price types from the catalog.
    /// </summary>
    public IReadOnlyList<PriceTypeInfo> LoadPriceTypes()
    {
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
        for (var i = 0; i < table.Count(); i++)
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

        _logger.LogInformation("Loaded {Count} price types.", result.Count);
        return result;
    }

    /// <summary>
    ///     Loads the latest known value of each (item, price_type) pair from the
    ///     raw prices register. Mirrors export1.py read_prices():
    ///     reads all records, keeps the latest per (item, type).
    ///     Returns: itemGuid -> { typeGuid -> (Цена, ПроцентСкидкиНаценки, unit, period) }.
    /// </summary>
    public Dictionary<string, Dictionary<string, (decimal Price, decimal MarkupPct, string Unit, string Period)>> LoadPrices()
    {
        var result = new Dictionary<string, Dictionary<string, (decimal, decimal, string, string)>>(StringComparer.OrdinalIgnoreCase);
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
                         РегистрСведений.ЦеныНоменклатуры AS Цены
                     """;

        dynamic table = query.Execute().Unload();
        for (var i = 0; i < table.Count(); i++)
        {
            dynamic row = table.Get(i);
            var itemGuid = GetRefGuid(row.Item);
            var typeGuid = GetRefGuid(row.PriceType);
            if (itemGuid is null || typeGuid is null)
            {
                continue;
            }

            var unit = GetRefName(row.Unit);
            var price = ToDecimal(row.Price);
            var markupPct = ToDecimal(row.MarkupPct);
            var period = FormatDateTime(row.Period);
            var entry = new ValueTuple<decimal, decimal, string, string>(price, markupPct, unit, period);

            if (!result.TryGetValue(itemGuid, out Dictionary<string, (decimal, decimal, string, string)>? types))
            {
                types = new Dictionary<string, (decimal, decimal, string, string)>(StringComparer.OrdinalIgnoreCase);
                result[itemGuid] = types;
            }

            // Keep the latest record for this (item, type).
            if (!types.TryGetValue(typeGuid, out (decimal, decimal, string, string) existing)
                || string.CompareOrdinal(entry.Item4, existing.Item4) > 0)
            {
                types[typeGuid] = entry;
            }
        }

        _logger.LogInformation("Loaded prices for {Count} items.", result.Count);
        return result;
    }

    /// <summary>
    ///     Loads current warehouse remainders (non-zero).
    ///     Returns a list of (itemGuid, warehouseGuid, warehouseName, quantity).
    /// </summary>
    public List<(string ItemGuid, string WarehouseGuid, string WarehouseName, decimal Quantity)> LoadStock()
    {
        var result = new List<(string, string, string, decimal)>();
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
                     """;

        dynamic table = query.Execute().Unload();
        for (var i = 0; i < table.Count(); i++)
        {
            dynamic row = table.Get(i);
            var itemGuid = GetRefGuid(row.Item);
            var whGuid = GetRefGuid(row.Warehouse);
            if (itemGuid is null || whGuid is null)
            {
                continue;
            }

            result.Add((itemGuid, whGuid, GetRefName(row.Warehouse), ToDecimal(row.Quantity)));
        }

        _logger.LogInformation("Loaded {Count} stock rows.", result.Count);
        return result;
    }

    /// <summary>
    ///     Loads the last movement date per item+warehouse.
    ///     Returns a dictionary keyed by "itemGuid_warehouseGuid".
    /// </summary>
    public Dictionary<string, string> LoadLastMovements()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        dynamic query = _session.Connection.NewObject("Query");
        query.Text = """
                     SELECT
                         Рух.Номенклатура AS Item,
                         Рух.Склад AS Warehouse,
                         MAX(Рух.Период) AS LastMovement
                     FROM
                         РегистрНакопления.ТоварыНаСкладах AS Рух
                     GROUP BY
                         Рух.Номенклатура,
                         Рух.Склад
                     """;

        dynamic table = query.Execute().Unload();
        for (var i = 0; i < table.Count(); i++)
        {
            dynamic row = table.Get(i);
            var itemGuid = GetRefGuid(row.Item);
            var whGuid = GetRefGuid(row.Warehouse);
            if (itemGuid is null || whGuid is null)
            {
                continue;
            }

            result[itemGuid + "_" + whGuid] = FormatDateTime(row.LastMovement);
        }

        _logger.LogInformation("Loaded {Count} last movement rows.", result.Count);
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
        IReadOnlyList<PriceTypeInfo> priceTypes,
        Dictionary<string, Dictionary<string, (decimal Price, decimal MarkupPct, string Unit, string Period)>> pricesByItem)
    {
        var result = new List<PriceEntry>();
        if (!pricesByItem.TryGetValue(itemGuid, out var itemPrices))
        {
            return result;
        }

        // Latest known cost price for this item (used as base for calculated types).
        decimal cost = 0m;
        foreach (var type in priceTypes)
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

        foreach (var type in priceTypes)
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

        return result;
    }

    /// <summary>
    ///     Builds the stock list for a given item GUID.
    /// </summary>
    public List<StockEntry> BuildStock(
        string itemGuid,
        List<(string ItemGuid, string WarehouseGuid, string WarehouseName, decimal Quantity)> stockRows,
        Dictionary<string, string> lastMovements)
    {
        var result = new List<StockEntry>();
        foreach (var row in stockRows)
        {
            if (!row.ItemGuid.Equals(itemGuid, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = itemGuid + "_" + row.WarehouseGuid;
            lastMovements.TryGetValue(key, out var lastMovement);

            result.Add(new StockEntry(
                row.WarehouseName,
                0, // status_code placeholder
                row.Quantity,
                lastMovement ?? string.Empty));
        }

        return result;
    }

    private string? GetRefGuid(object? refObject)
    {
        if (refObject is null || refObject is DBNull)
        {
            return null;
        }

        if (!Marshal.IsComObject(refObject))
        {
            return null;
        }

        // Extract the GUID via УникальныйИдентификатор (like CatalogReader.GetRefId).
        // String(ref) in 1C returns the display name, not the GUID.
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
                return _session.String(guid);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("УникальныйИдентификатор failed: {Message}", ex.Message);
        }

        return null;
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

        try
        {
            var type = refObject.GetType();
            var name = type.InvokeMember("Description", BindingFlags.GetProperty, null, refObject, null);
            return name?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
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