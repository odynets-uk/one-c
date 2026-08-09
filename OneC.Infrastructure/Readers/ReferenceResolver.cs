using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Resolves GUIDs and names from 1C COM reference objects with caching.
///     Uses ConditionalWeakTable for per-RCW caching and IUnknown pointer mapping for O(1) lookups.
/// </summary>
public sealed class ReferenceResolver
{
    private readonly IComSession _session;
    private readonly ILogger _logger;

    // Cache COM ref object -> GUID string. Resolves each unique reference once,
    // avoiding repeated (very expensive) УникальныйИдентификатор COM round-trips
    // for the same item/price-type appearing in many rows or across batches.
    private readonly ConditionalWeakTable<object, string?> _guidCache = new();
    private readonly ConditionalWeakTable<object, string> _nameCache = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceResolver" /> class.
    /// </summary>
    public ReferenceResolver(IComSession session, ILogger logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>
    ///     Extracts the GUID from a 1C COM reference object.
    ///     Uses the reverse IUnknown map for O(1) lookup when refCache is provided.
    ///     Falls back to per-RCW cache, then to УникальныйИдентификатор COM call.
    /// </summary>
    public string? GetRefGuid(object? refObject, RefCache? refCache = null)
    {
        if (refObject is null || refObject is DBNull)
        {
            return null;
        }

        if (!Marshal.IsComObject(refObject))
        {
            return null;
        }

        // Fast path: resolve the GUID via the reverse IUnknown map — O(1), no COM
        // round-trip per row. Marshal.GetIUnknownForObject returns the pointer of
        // the underlying COM object itself (shared across all RCW wrappers for the
        // same 1C reference), so a row.Item from a ValueTable hits the same key
        // as the ref built by BuildRefCache.
        if (refCache is not null)
        {
            var iUnknown = Marshal.GetIUnknownForObject(refObject);
            if (refCache.ByIUnknown.TryGetValue(iUnknown, out var cachedGuid))
            {
                return cachedGuid;
            }
        }

        // Fallback: cache per RCW object, then resolve via УникальныйИдентификатор.
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

    /// <summary>
    ///     Extracts the display name (Description) from a 1C COM reference object.
    /// </summary>
    public string GetRefName(object? refObject)
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
}