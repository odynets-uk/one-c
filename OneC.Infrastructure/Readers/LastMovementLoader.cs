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
    private readonly RefArrayFactory _refArrayFactory;
    private readonly ILogger _logger;

    // Batch size for the 1C array of references (В (&ItemsArray)).
    // Smaller batches = faster per-query execution in 1C. 2000 refs per batch
    // keeps GROUP BY queries responsive without overloading the server.
    private const int RefBatchSize = 2000;

    public LastMovementLoader(IComSession session, ReferenceResolver referenceResolver, RefArrayFactory refArrayFactory, ILogger logger)
    {
        _session = session;
        _referenceResolver = referenceResolver;
        _refArrayFactory = refArrayFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Loads the last movement date per item+warehouse.
    ///     Filters by a 1C array of references (В (&ItemsArray)).
    ///     Returns a dictionary keyed by "itemGuid_warehouseGuid".
    ///     When <paramref name="changedSince" /> is provided, only movements within
    ///     the period are considered — items with no movement in the window are excluded.
    /// </summary>
    public Dictionary<string, string> LoadLastMovements(
        IReadOnlyCollection<string> itemGuids,
        RefCache refCache,
        string? changedSince = null)
    {
        var sw = Stopwatch.StartNew();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var (sinceDate, toDate) = PeriodParser.Parse(changedSince);

        foreach (var batch in itemGuids.Chunk(RefBatchSize))
        {
            dynamic query = _session.Connection.NewObject("Query");

            var text = """
                       SELECT
                           Рух.Номенклатура AS Item,
                           Рух.Склад AS Warehouse,
                           MAX(Рух.Период) AS LastMovement
                       FROM
                           РегистрНакопления.ТоварыНаСкладах AS Рух
                       WHERE
                           Рух.Номенклатура В (&ItemsArray)
                       """;

            if (sinceDate is not null)
            {
                text += "\n    AND Рух.Период >= &SinceDate";
                if (toDate is not null)
                {
                    text += "\n    AND Рух.Период <= &ToDate";
                }
            }

            text += """
                    
                    GROUP BY
                        Рух.Номенклатура,
                        Рух.Склад
                    """;

            query.Text = text;
            query.SetParameter("ItemsArray", _refArrayFactory.CreateRefArray(batch, refCache));
            if (sinceDate is not null)
            {
                query.SetParameter("SinceDate", sinceDate.Value);
                if (toDate is not null)
                {
                    query.SetParameter("ToDate", toDate.Value);
                }
            }

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
    ///     Returns the distinct item GUIDs that had a stock movement within the given
    ///     period. Reads the PHYSICAL register table with a period filter — no item
    ///     filter, because the changed GUIDs are unknown yet.
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
                       Рух.Номенклатура AS Item
                   FROM
                       РегистрНакопления.ТоварыНаСкладах AS Рух
                   WHERE
                       Рух.Период >= &SinceDate
                   """;

        if (toDate is not null)
        {
            text += "\n    AND Рух.Период <= &ToDate";
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

        _logger.LogInformation("Loaded {Count} changed item GUIDs (stock) in {ElapsedMs} ms.", result.Count, sw.ElapsedMilliseconds);
        return result;
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