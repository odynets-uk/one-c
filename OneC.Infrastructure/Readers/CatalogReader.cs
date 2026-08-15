using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneC.Application.Abstractions;
using OneC.Domain.Profiles;
using OneC.Domain.ValueObjects;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Reads catalog data from 1C via COM using a profile definition.
///     Uses Query (SELECT) for streaming reads — Cyrillic methods work via InvokeMember.
/// </summary>
public sealed class CatalogReader
{
    private readonly IComSession _session;
    private readonly ComValueMapper _mapper;
    private readonly ILogger<CatalogReader> _logger;
    private readonly IRegisterDataReader _registerReader;
    private readonly ReferenceResolver _referenceResolver;
    private readonly RefArrayFactory _refArrayFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CatalogReader" /> class.
    /// </summary>
    /// <param name="session">An established COM session.</param>
    /// <param name="mapper">Value mapper.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="registerReader">Register data reader.</param>
    /// <param name="referenceResolver">Reference resolver for cached GUID extraction.</param>
    public CatalogReader(IComSession session, ComValueMapper mapper, ILogger<CatalogReader> logger, IRegisterDataReader registerReader, ReferenceResolver referenceResolver, RefArrayFactory refArrayFactory)
    {
        _session = session;
        _mapper = mapper;
        _logger = logger;
        _registerReader = registerReader;
        _referenceResolver = referenceResolver;
        _refArrayFactory = refArrayFactory;
    }

    /// <summary>
    ///     Reads catalog records using the given profile.
    /// </summary>
    /// <param name="profile">Extraction profile.</param>
    /// <param name="batchSize">Batch size (-1 = all records).</param>
    /// <returns>List of mapped records (dictionary: column name → value).</returns>
    public IReadOnlyList<Dictionary<string, object?>> Read(ExtractionProfile profile, int batchSize = -1)
    {
        var catalogType = profile.Source?.Type ?? profile.RootType ?? string.Empty;
        var catalogName = ExtractCatalogName(catalogType);
        var tableName = profile.Table ?? InferTableName(profile);
        var records = new List<Dictionary<string, object?>>();

        // Whether to read prices/stock from registers.
        var readPrices = profile.Filters?.Prices is not null;
        var readStock = profile.Filters?.Stock is not null;
        var pricesChangedSince = profile.Filters?.Prices?.ChangedSince;
        var stockChangedSince = profile.Filters?.Stock?.ChangedSince;
        var hasChangedSince = pricesChangedSince is not null || stockChangedSince is not null;

        try
        {
            // 0. Load the set of category GUIDs (IsFolder = true) for 'exists' validation.
            //    Categories and products come from the same catalog (Номенклатура);
            //    products reference categories via Parent → category_id.
            var categorySw = Stopwatch.StartNew();
            var categoryIdSet = LoadCategoryGuids(catalogName);
            categorySw.Stop();
            _logger.LogInformation("Stage category-guids done in {ElapsedMs} ms.", categorySw.ElapsedMilliseconds);

            // 0b. For changed_since: pre-filter the catalog to only changed items.
            //     Load the changed GUIDs from registers (prices OR stock), build the
            //     ref cache for them, then read the catalog with WHERE Ref IN (...).
            //     This avoids iterating the entire catalog (15k+ records) when only
            //     a few hundred items changed.
            RefCache? refCache = null;
            if (hasChangedSince)
            {
                var changedSw = Stopwatch.StartNew();
                var changedGuids = _registerReader.LoadChangedItemGuids(pricesChangedSince, stockChangedSince);
                changedSw.Stop();
                _logger.LogInformation("Stage changed-guids done: {Count} GUIDs in {ElapsedMs} ms.", changedGuids.Count, changedSw.ElapsedMilliseconds);

                var refCacheSw = Stopwatch.StartNew();
                refCache = _registerReader.BuildRefCache(changedGuids, catalogName);
                refCacheSw.Stop();
                _logger.LogInformation("Stage ref-cache done in {ElapsedMs} ms.", refCacheSw.ElapsedMilliseconds);
            }

            // 1. Create a Query object (Latin method — works via dynamic).
            dynamic query = _session.Connection.NewObject("Query");

            // 2. Build SELECT and WHERE clauses from the profile.
            var selectClause = BuildSelectClause(profile, catalogName);
            var whereClause = BuildWhereClause(profile, catalogName);

            // For changed_since, restrict the catalog read to the changed items only.
            if (hasChangedSince && refCache is not null)
            {
                var refIn = $"{catalogName}.Ref IN (&ChangedItems)";
                whereClause = whereClause.Length > 0
                    ? $"{whereClause} AND {refIn}"
                    : $"WHERE {refIn}";
            }

            query.Text = $"SELECT {selectClause} FROM Справочник.{catalogName} AS {catalogName} {whereClause}";

            if (hasChangedSince && refCache is not null)
            {
                query.SetParameter("ChangedItems", _refArrayFactory.CreateRefArray(refCache.ByGuid.Keys, refCache));
            }

            _logger.LogInformation("Executing query: {QueryText}", (string)query.Text);

            // 3. Execute and unload to ValueTable for faster access.
            var iterSw = Stopwatch.StartNew();
            dynamic queryResult = query.Execute();
            dynamic table = queryResult.Unload();

            var rowCount = table.Count();
            var itemGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < rowCount; i++)
            {
                dynamic selection = table.Get(i);
                var record = MapRecord(selection, profile, catalogName, categoryIdSet);

                if (readPrices || readStock)
                {
                    var itemGuid = record.TryGetValue("id", out object? idVal) ? idVal?.ToString() : null;
                    if (itemGuid is not null)
                    {
                        itemGuids.Add(itemGuid);
                    }
                }

                records.Add(record);

                if ((i + 1) % 1000 == 0)
                {
                    _logger.LogInformation("Iterated {Count} catalog records...", i + 1);
                }

                if (batchSize > 0 && (i + 1) >= batchSize)
                {
                    _logger.LogInformation("Batch limit reached: {BatchSize} records.", batchSize);
                    break;
                }
            }
            iterSw.Stop();
            _logger.LogInformation("Stage catalog-iterate done: {Count} records in {ElapsedMs} ms.", records.Count, iterSw.ElapsedMilliseconds);

            // 4b. Load price/stock data from registers. The registers are filtered
            //     by a 1C array of references (В (&ItemsArray)) for the collected
            //     item GUIDs — avoids loading the entire register.
            IReadOnlyDictionary<string, PriceTypeInfo>? priceTypesByGuid = null;
            Dictionary<string, Dictionary<string, PriceRow>>? pricesByItem = null;
            Dictionary<string, List<StockRow>>? stockByItem = null;
            Dictionary<string, string>? lastMovements = null;

            if (readPrices || readStock)
            {
                // Build the GUID -> COM reference cache ONCE and reuse it
                // across LoadPrices/LoadStock/LoadLastMovements.
                // For changed_since, the cache was already built in stage 0b.
                var registerRefCache = refCache;
                if (registerRefCache is null)
                {
                    var refCacheSw = Stopwatch.StartNew();
                    registerRefCache = _registerReader.BuildRefCache(itemGuids, catalogName);
                    refCacheSw.Stop();
                    _logger.LogInformation("Stage ref-cache done in {ElapsedMs} ms.", refCacheSw.ElapsedMilliseconds);
                }

            if (readPrices)
            {
                var priceTypesSw = Stopwatch.StartNew();
                var priceTypes = _registerReader.LoadPriceTypes();
                priceTypesByGuid = priceTypes.ToDictionary(t => t.Guid, StringComparer.OrdinalIgnoreCase);
                priceTypesSw.Stop();
                _logger.LogInformation("Stage price-types done in {ElapsedMs} ms.", priceTypesSw.ElapsedMilliseconds);

                var pricesSw = Stopwatch.StartNew();
                pricesByItem = _registerReader.LoadPrices(itemGuids, registerRefCache, profile.Filters?.Prices?.ChangedSince);
                pricesSw.Stop();
                _logger.LogInformation("Stage prices done in {ElapsedMs} ms.", pricesSw.ElapsedMilliseconds);
            }

            if (readStock)
            {
                var stockSw = Stopwatch.StartNew();
                stockByItem = _registerReader.LoadStock(itemGuids, registerRefCache);
                stockSw.Stop();
                _logger.LogInformation("Stage stock done in {ElapsedMs} ms.", stockSw.ElapsedMilliseconds);

                var lastMovSw = Stopwatch.StartNew();
                lastMovements = _registerReader.LoadLastMovements(itemGuids, registerRefCache, profile.Filters?.Stock?.ChangedSince);
                lastMovSw.Stop();
                _logger.LogInformation("Stage last-movements done in {ElapsedMs} ms.", lastMovSw.ElapsedMilliseconds);
            }
            }

            // 5. Attach prices/stock to the records (if requested).
            //    Independent filtering: when changed_since is set for prices and/or stock,
            //    an item is kept if it satisfies AT LEAST ONE of the active filters (OR),
            //    so a price change keeps the item even if stock didn't move, and vice versa.
            if (readPrices || readStock)
            {
                var pricesFilterActive = pricesChangedSince is not null;
                var stockFilterActive = stockChangedSince is not null;

                var filtered = new List<Dictionary<string, object?>>(records.Count);
                foreach (var record in records)
                {
                    var itemGuid = record.TryGetValue("id", out object? idVal) ? idVal?.ToString() : null;
                    var hasPrices = false;
                    var hasStock = false;

                    if (itemGuid is not null)
                    {
                        if (readPrices && priceTypesByGuid is not null && pricesByItem is not null)
                        {
                            var prices = _registerReader.BuildPrices(itemGuid, priceTypesByGuid, pricesByItem);
                            record["prices"] = prices.Count > 0 ? prices : null;
                            hasPrices = prices.Count > 0;
                        }

                        if (readStock && stockByItem is not null && lastMovements is not null)
                        {
                            var stock = _registerReader.BuildStock(itemGuid, stockByItem, lastMovements);

                            // When changed_since is set for stock, keep only warehouses
                            // that had movement within the period (LoadLastMovements already
                            // filtered by period, so empty LastMovement = no movement in window).
                            if (stockFilterActive)
                            {
                                stock = stock.Where(s => !string.IsNullOrEmpty(s.LastMovement)).ToList();
                            }

                            record["stock"] = stock.Count > 0 ? stock : null;
                            hasStock = stock.Count > 0;
                        }
                    }

                    // Determine whether to keep the item.
                    var keep = true;
                    if (pricesFilterActive && stockFilterActive)
                    {
                        // OR: keep if prices changed OR stock changed.
                        keep = hasPrices || hasStock;
                    }
                    else if (pricesFilterActive)
                    {
                        keep = hasPrices;
                    }
                    else if (stockFilterActive)
                    {
                        keep = hasStock;
                    }
                    else
                    {
                        // Legacy skip flags (no changed_since).
                        if (profile.SkipItemsWithoutPrices && readPrices && !hasPrices)
                        {
                            keep = false;
                        }

                        if (profile.SkipItemsWithoutStock && readStock && !hasStock)
                        {
                            keep = false;
                        }
                    }

                    if (keep)
                    {
                        filtered.Add(record);
                    }
                }

                records = filtered;
            }

            _logger.LogInformation(
                "Read {Count} records from catalog '{CatalogName}' (table '{Table}').",
                records.Count,
                catalogName,
                tableName);
        }
        catch (COMException ex)
        {
            _logger.LogError(ex, "Failed to read catalog '{CatalogName}'. HRESULT: {HResult}", catalogName, ex.HResult);
            throw new InvalidOperationException($"Failed to read catalog '{catalogName}'.", ex);
        }

        return records;
    }

    /// <summary>
    ///     Loads the GUIDs of all categories (IsFolder = true) from the catalog.
    ///     Categories and products live in the same catalog (Номенклатура);
    ///     products reference categories via Parent. Used for 'exists' validation.
    /// </summary>
    private HashSet<string> LoadCategoryGuids(string catalogName)
    {
        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        dynamic query = _session.Connection.NewObject("Query");
        query.Text = $"SELECT {catalogName}.Ref AS Ref FROM Справочник.{catalogName} AS {catalogName} WHERE {catalogName}.IsFolder = TRUE";
        dynamic result = query.Execute();
        dynamic selection = result.Choose();
        while (selection.Next())
        {
            var refObj = selection.Ref;
            if (refObj is not null)
            {
                var guid = GetRefId(refObj);
                if (guid is not null)
                {
                    guids.Add(guid);
                }
            }
        }
        _logger.LogInformation("Loaded {Count} category GUIDs (IsFolder=true) from '{CatalogName}'.", guids.Count, catalogName);
        return guids;
    }

    private static string ExtractCatalogName(string catalogType)
    {
        // "CatalogObject.Номенклатура" → "Номенклатура"
        if (catalogType.Contains('.', StringComparison.Ordinal))
        {
            return catalogType[(catalogType.LastIndexOf('.') + 1)..];
        }

        return catalogType;
    }

    private static string InferTableName(ExtractionProfile profile)
    {
        return (profile.Name ?? "data").ToLowerInvariant();
    }

    private static string BuildSelectClause(ExtractionProfile profile, string alias)
    {
        var parts = new List<string>();

        // Always include Ref (for GUID).
        parts.Add($"{alias}.Ref AS __Ref");

        // Add fields from the profile columns.
        foreach (var column in profile.Columns)
        {
            if (!column.Source.Equals("Ref", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add($"{alias}.{column.Source} AS {column.Source}");
            }
        }

        return string.Join(", ", parts);
    }

    private static string BuildWhereClause(ExtractionProfile profile, string alias)
    {
        var conditions = new List<string>();

        // field_filters: { "IsFolder": true, "Code": ["000002841", ...], "Description": "..." }
        var fieldFilters = profile.Filters?.FieldFilters;
        if (fieldFilters is not null)
        {
            foreach (var filter in fieldFilters)
            {
                conditions.Add(BuildFieldCondition(alias, filter.Key, filter.Value));
            }
        }

        return conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : string.Empty;
    }

    private static string BuildFieldCondition(string alias, string field, object? value)
    {
        // JsonElement array (from JSON deserialization of field_filters) → IN (...)
        if (value is System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.Array } arr)
        {
            var items = arr.EnumerateArray().Select(je => FormatFilterValue((object)je)).ToList();
            return items.Count > 0
                ? $"{alias}.{field} IN ({string.Join(", ", items)})"
                : "1 = 0"; // empty array → no rows
        }

        // Array → IN (...)
        if (value is System.Collections.IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object?>().Select(FormatFilterValue).ToList();
            return items.Count > 0
                ? $"{alias}.{field} IN ({string.Join(", ", items)})"
                : "1 = 0"; // empty array → no rows
        }

        // Description → partial match (1C query language uses double quotes: LIKE "%...%")
        if (field.Equals("Description", StringComparison.OrdinalIgnoreCase))
        {
            return $"{alias}.{field} LIKE \"%{value}%\"";
        }

        return $"{alias}.{field} = {FormatFilterValue(value)}";
    }

    private static string FormatFilterValue(object? value)
    {
        return value switch
        {
            bool b => b ? "TRUE" : "FALSE",
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.True } => "TRUE",
            System.Text.Json.JsonElement { ValueKind: System.Text.Json.JsonValueKind.False } => "FALSE",
            System.Text.Json.JsonElement json => json.ValueKind == System.Text.Json.JsonValueKind.String
                ? $"\"{json.GetString()}\""
                : json.ToString(),
            null => "NULL",
            _ => $"\"{value}\"",
        };
    }

    private Dictionary<string, object?> MapRecord(
        dynamic selection,
        ExtractionProfile profile,
        string alias,
        HashSet<string> idSet)
    {
        var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // If columns are defined in the profile — map according to them.
        if (profile.Columns.Count > 0)
        {
            foreach (var column in profile.Columns)
            {
                object? rawValue = null;
                try
                {
                    // Ref: use the __Ref alias from SELECT (accessible via dynamic, like __SystemGuid in 1c_ex).
                    if (column.Source.Equals("Ref", StringComparison.OrdinalIgnoreCase))
                    {
                        var refObj = selection.__Ref;
                        rawValue = refObj is null ? null : GetRefId(refObj);
                    }
                    else
                    {
                        rawValue = GetProperty(selection, column.Source);

                        // Convert COM reference objects (e.g. Parent) to GUID strings.
                        if (rawValue is not null && rawValue.GetType().IsCOMObject)
                        {
                            // Try to extract GUID (for Ref fields like Parent).
                            // OneCRef.FromString returns null for an empty reference (all-zero GUID) → null.
                            var guid = GetRefId(rawValue);
                            if (guid is not null)
                            {
                                rawValue = guid;
                            }
                            else
                            {
                                // Not a Ref or empty reference — convert to string (empty/{} → null).
                                var str = _session.String(rawValue);
                                rawValue = string.IsNullOrWhiteSpace(str) || str == "{}" ? null : str;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to read field '{Field}': {Message}", column.Source, ex.Message);
                }

                try
                {
                    var mapped = _mapper.Map(rawValue, column);
                    record[column.Name] = ConvertToDbValue(mapped, column);

                    // 'exists' runtime validation: value must be null or exist in the loaded category guid set.
                    if (column.Validation?.Exists is not null && mapped is not null)
                    {
                        var existsRef = column.Validation.Exists;
                        if (!idSet.Contains(mapped.ToString()!))
                        {
                            throw new InvalidOperationException(
                                $"Column '{column.Name}': value '{mapped}' does not exist in '{existsRef}'.");
                        }
                    }
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex.Message);
                    record[column.Name] = DBNull.Value;
                }
            }

            return record;
        }

        // Fallback: no columns defined — map Ref, Code, Description heuristically.
        record["Ref"] = GetRefId(selection.__Ref);
        record["Code"] = GetProperty(selection, "Code")?.ToString();
        record["Description"] = GetProperty(selection, "Description")?.ToString();
        return record;
    }

    private static object? GetProperty(dynamic obj, string propertyName)
    {
        // InvokeMember works for both Latin and Cyrillic field names.
        // Do NOT call obj.Get(propertyName) first — for Cyrillic fields it always
        // throws a (very expensive) COM-interop exception before falling back here.
        var type = ((object)obj).GetType();
        return type.InvokeMember(
            propertyName,
            BindingFlags.GetField | BindingFlags.GetProperty,
            null,
            obj,
            null);
    }

    /// <summary>
    ///     Extracts the GUID from a 1C COM reference object.
    ///     String(ref) in 1C returns the display name (Description),
    ///     so we must call УникальныйИдентификатор / Ref to get the actual GUID.
    ///     An empty reference (all-zero GUID) is returned as null.
    /// </summary>
    private string? GetRefId(object refObject)
    {
        // Use the injected ReferenceResolver for cached, high-performance GUID extraction.
        // This replaces the slow static implementation that called УникальныйИдентификатор every time.
        return _referenceResolver?.GetRefGuid(refObject);
    }

    private static object? ConvertToDbValue(object? value, ProfileColumn column)
    {
        if (value is null)
        {
            return DBNull.Value;
        }

        // SQLite INTEGER for boolean columns.
        if (value is bool b && column.SqlType.StartsWith("INTEGER", StringComparison.OrdinalIgnoreCase))
        {
            return b ? 1 : 0;
        }

        return value;
    }
}