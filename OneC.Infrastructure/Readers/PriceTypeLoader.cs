using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Logging;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Loads price types from the 1C catalog (Справочник.ТипыЦенНоменклатуры).
/// </summary>
public sealed class PriceTypeLoader
{
    private readonly IComSession _session;
    private readonly ILogger _logger;

    public PriceTypeLoader(IComSession session, ILogger logger)
    {
        _session = session;
        _logger = logger;
    }

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

    private string? GetRefGuid(object? refObject)
    {
        if (refObject is null || refObject is DBNull)
        {
            return null;
        }

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
        catch (Exception)
        {
            // Ignore
        }

        return null;
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