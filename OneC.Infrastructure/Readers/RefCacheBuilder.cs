using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Builds a cache of GUID -> COM reference (1C catalog item Ref objects).
///     The refs are created once and reused by LoadPrices/LoadStock/LoadLastMovements
///     — avoids building identical reference arrays three times.
/// </summary>
public sealed class RefCacheBuilder
{
    private readonly IComSession _session;
    private readonly ILogger _logger;

    public RefCacheBuilder(IComSession session, ILogger logger)
    {
        _session = session;
        _logger = logger;
    }

    /// <summary>
    ///     Builds a cache of GUID -> COM reference, plus a reverse map IUnknown pointer -> GUID.
    ///     The reverse map lets us resolve item/price-type refs returned by queries
    ///     in O(1) WITHOUT a COM round-trip (УникальныйИдентификатор call per row),
    ///     which was the main bottleneck on the full base (~40k COM calls = ~250s).
    /// </summary>
    public RefCache BuildRefCache(
        IReadOnlyCollection<string> itemGuids,
        string catalogName)
    {
        var sw = Stopwatch.StartNew();
        var byGuid = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var byIUnknown = new Dictionary<IntPtr, string>();

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

            // Map the underlying COM object's IUnknown pointer -> GUID.
            // Marshal.GetIUnknownForObject returns the pointer of the COM object
            // itself (shared across all RCW wrappers for the same 1C reference),
            // so a row.Item from a ValueTable resolves to the same key.
            // NOTE: we do NOT Marshal.Release here — the RCW keeps the COM object
            // alive, and the pointer stays valid for the session.
            var iUnknown = Marshal.GetIUnknownForObject(refObj);
            byIUnknown[iUnknown] = guid;
        }

        _logger.LogInformation("Built {Count} reference cache entries in {ElapsedMs} ms.", byGuid.Count, sw.ElapsedMilliseconds);
        return new RefCache { ByGuid = byGuid, ByIUnknown = byIUnknown };
    }
}