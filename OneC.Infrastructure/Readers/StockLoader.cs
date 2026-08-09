using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Loads stock remainders from the 1C accumulation register (РегистрНакопления.ТоварыНаСкладах.Остатки).
/// </summary>
public sealed class StockLoader
{
    private readonly IComSession _session;
    private readonly ReferenceResolver _referenceResolver;
    private readonly ILogger _logger;

    // Batch size for the 1C array of references (В (&ItemsArray)).
    // Smaller batches = faster per-query execution in 1C. 2000 refs per batch
    // keeps each query responsive without overloading the server.
    private const int RefBatchSize = 2000;

    public StockLoader(IComSession session, ReferenceResolver referenceResolver, ILogger logger)
    {
        _session = session;
        _referenceResolver = referenceResolver;
        _logger = logger;
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
                var itemGuid = _referenceResolver.GetRefGuid(row.Item, refCache);
                var whGuid = _referenceResolver.GetRefGuid(row.Warehouse);
                if (itemGuid is null || whGuid is null)
                {
                    continue;
                }

                if (!result.TryGetValue(itemGuid, out List<StockRow>? rows))
                {
                    rows = new List<StockRow>();
                    result[itemGuid] = rows;
                }

                rows!.Add(new StockRow(whGuid, _referenceResolver.GetRefName(row.Warehouse), ToDecimal(row.Quantity)));
            }
        }

        _logger.LogInformation("Loaded stock for {Count} items in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
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
}