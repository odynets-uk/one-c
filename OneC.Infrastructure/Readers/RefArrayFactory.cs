using OneC.Domain.Register;
using OneC.Infrastructure.Com;

namespace OneC.Infrastructure.Readers;

/// <summary>
///     Creates 1C Массив of catalog references from a GUID batch,
///     reusing an already-built reference cache.
/// </summary>
public sealed class RefArrayFactory
{
    private readonly IComSession _session;

    public RefArrayFactory(IComSession session)
    {
        _session = session;
    }

    /// <summary>
    ///     Creates a 1C Массив of catalog references for the given GUID batch,
    ///     reusing the already-built reference cache.
    /// </summary>
    public dynamic CreateRefArray(IEnumerable<string> guids, RefCache refCache)
    {
        dynamic v8Array = _session.Connection.NewObject("Массив");
        foreach (var guid in guids)
        {
            v8Array.Add(refCache.ByGuid[guid]);
        }

        return v8Array;
    }
}