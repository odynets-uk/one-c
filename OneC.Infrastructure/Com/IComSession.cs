namespace OneC.Infrastructure.Com;

/// <summary>
///     Abstraction over a 1C COM session, used to decouple value mapping from the concrete COM session.
/// </summary>
public interface IComSession
{
    /// <summary>
    ///     Gets the underlying dynamic connection object.
    /// </summary>
    dynamic Connection { get; }

    /// <summary>
    ///     Converts a COM value (e.g. Ref/GUID) to a string using the 1C String() function.
    /// </summary>
    /// <param name="value">COM value.</param>
    /// <returns>String representation.</returns>
    string String(object value);
}