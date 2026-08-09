using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Loads last movement dates from the 1C accumulation register (РегистрНакопления.ТоварыНаСкладах).
/// </summary>
public sealed class LastMovementLoader
{
    private readonly IComSession _session;
    private readonly ReferenceResolver _referenceResolver;
    private readonly ILogger _logger;

    // Batch size for the 1C array of references (В (&ItemsArray)).
    // Smaller batches = faster per-query execution in 1C. 2000 refs per batch
    // keeps GROUP BY queries responsive without overloading the server.
    private const int RefBatchSize = 2000;

    public LastMovementLoader(IComSession session, ReferenceResolver referenceResolver, ILogger logger)
    {
        _session = session;
        _referenceResolver = referenceResolver;
        _logger = logger;
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
                var itemGuid = _referenceResolver.GetRefGuid(row.Item, refCache);
                var whGuid = _referenceResolver.GetRefGuid(row.Warehouse);
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