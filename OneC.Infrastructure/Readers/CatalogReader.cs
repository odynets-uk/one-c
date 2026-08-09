using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneC.Domain.Profiles;
using OneC.Domain.ValueObjects;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Reads catalog data from 1C via COM using a profile definition.
///     Uses Query (SELECT) for streaming reads — Cyrillic methods work via InvokeMember.
/// </summary>
public sealed class CatalogReader
{
    private readonly ComSession _session;
    private readonly ComValueMapper _mapper;
    private readonly ILogger<CatalogReader> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CatalogReader" /> class.
    /// </summary>
    /// <param name="session">An established COM session.</param>
    /// <param name="mapper">Value mapper.</param>
    /// <param name="logger">Logger instance.</param>
    public CatalogReader(ComSession session, ComValueMapper mapper, ILogger<CatalogReader> logger)
    {
        _session = session;
        _mapper = mapper;
        _logger = logger;
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

        try
        {
            // 1. Create a Query object (Latin method — works via dynamic).
            dynamic query = _session.Connection.NewObject("Query");

            // 2. Build SELECT and WHERE clauses from the profile.
            var selectClause = BuildSelectClause(profile, catalogName);
            var whereClause = BuildWhereClause(profile, catalogName);

            query.Text = $"SELECT {selectClause} FROM Справочник.{catalogName} AS {catalogName} {whereClause}";

            _logger.LogInformation("Executing query: {QueryText}", (string)query.Text);

            // 3. Execute (Latin methods).
            dynamic queryResult = query.Execute();
            dynamic selection = queryResult.Choose();

            // 4. Iterate (Latin method).
            var count = 0;
            var idSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            while (selection.Next())
            {
                var record = MapRecord(selection, profile, catalogName, idSet);
                records.Add(record);
                count++;

                if (batchSize > 0 && count >= batchSize)
                {
                    _logger.LogInformation("Batch limit reached: {BatchSize} records.", batchSize);
                    break;
                }
            }

            _logger.LogInformation(
                "Read {Count} records from catalog '{CatalogName}' (table '{Table}').",
                count,
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

                    // Collect the 'id' column values for 'exists' validation.
                    if (column.Name.Equals("id", StringComparison.OrdinalIgnoreCase) && mapped is not null)
                    {
                        idSet.Add(mapped.ToString()!);
                    }

                    // 'exists' runtime validation: value must be null or exist in the collected id set.
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
        // Try dynamic access first (works for Latin aliases like __Ref).
        try
        {
            return obj.Get(propertyName);
        }
        catch (Exception)
        {
            // Fallback to InvokeMember for Cyrillic field names.
            var type = ((object)obj).GetType();
            return type.InvokeMember(
                propertyName,
                BindingFlags.GetField | BindingFlags.GetProperty,
                null,
                obj,
                null);
        }
    }

    /// <summary>
    ///     Extracts the GUID from a 1C COM reference object.
    ///     String(ref) in 1C returns the display name (Description),
    ///     so we must call УникальныйИдентификатор / Ref to get the actual GUID.
    ///     An empty reference (all-zero GUID) is returned as null.
    /// </summary>
    private string? GetRefId(object refObject)
    {
        var type = refObject.GetType();

        // 1) Try УникальныйИдентификатор() (Cyrillic, works via InvokeMember).
        try
        {
            var guid = type.InvokeMember(
                "УникальныйИдентификатор",
                BindingFlags.InvokeMethod,
                null,
                refObject,
                null);
            if (guid is not null)
            {
                return OneCRef.FromString(_session.String(guid))?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("УникальныйИдентификатор failed: {Message}", ex.Message);
        }

        // 2) Try Ref property (latin) — returns the Ref value.
        try
        {
            var refValue = type.InvokeMember(
                "Ref",
                BindingFlags.GetProperty,
                null,
                refObject,
                null);
            if (refValue is not null && !refValue.GetType().IsCOMObject)
            {
                return OneCRef.FromString(_session.String(refValue))?.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Ref property failed: {Message}", ex.Message);
        }

        _logger.LogWarning("Failed to extract GUID from 1C reference object.");
        return null;
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