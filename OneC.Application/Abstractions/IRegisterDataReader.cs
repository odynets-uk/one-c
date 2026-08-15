using System.Collections.Generic;
using OneC.Domain.Register;

namespace OneC.Application.Abstractions;

/// <summary>
///     Port for reading price and stock data from 1C registers.
/// </summary>
public interface IRegisterDataReader
{
    /// <summary>
    ///     Loads all price types from the catalog.
    /// </summary>
    IReadOnlyList<PriceTypeInfo> LoadPriceTypes();

    /// <summary>
    ///     Returns the union of item GUIDs that changed within the given periods
    ///     (prices OR stock movements). A null period means that register is not
    ///     considered. Used to pre-filter the catalog before reading it.
    /// </summary>
    IReadOnlyCollection<string> LoadChangedItemGuids(
        string? pricesChangedSince,
        string? stockChangedSince);

    /// <summary>
    ///     Builds a cache of GUID -> COM reference for the given item GUIDs.
    /// </summary>
    RefCache BuildRefCache(IReadOnlyCollection<string> itemGuids, string catalogName);

    /// <summary>
    ///     Loads the latest price slice (СрезПоследних) for the given items.
    ///     When <paramref name="changedSince" /> is provided, only prices that changed
    ///     within the period are returned (latest per item+type).
    /// </summary>
    Dictionary<string, Dictionary<string, PriceRow>> LoadPrices(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache,
        string? changedSince = null);

    /// <summary>
    ///     Loads current warehouse remainders (non-zero) for the given items.
    /// </summary>
    Dictionary<string, List<StockRow>> LoadStock(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache);

    /// <summary>
    ///     Loads the last movement date per item+warehouse for the given items.
    ///     When <paramref name="changedSince" /> is provided, only movements within
    ///     the period are considered.
    /// </summary>
    Dictionary<string, string> LoadLastMovements(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache,
        string? changedSince = null);

    /// <summary>
    ///     Builds the prices list for a given item GUID.
    /// </summary>
    List<PriceEntry> BuildPrices(
        string itemGuid,
        IReadOnlyDictionary<string, PriceTypeInfo> priceTypesByGuid,
        Dictionary<string, Dictionary<string, PriceRow>> pricesByItem);

    /// <summary>
    ///     Builds the stock list for a given item GUID.
    /// </summary>
    List<StockEntry> BuildStock(
        string itemGuid,
        Dictionary<string, List<StockRow>> stockByItem,
        Dictionary<string, string> lastMovements);
}