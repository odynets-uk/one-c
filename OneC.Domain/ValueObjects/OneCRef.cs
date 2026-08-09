namespace OneC.Domain.ValueObjects;

/// <summary>
///     Represents a 1C reference (Ref) as a value object.
///     Encapsulates the logic of parsing and normalizing 1C reference GUIDs:
///     an empty reference (all-zero GUID) is treated as null.
/// </summary>
public readonly record struct OneCRef
{
    /// <summary>
    ///     The 1C empty reference GUID (all zeros).
    /// </summary>
    public static readonly Guid Empty = Guid.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OneCRef" /> struct.
    /// </summary>
    /// <param name="value">The reference GUID.</param>
    private OneCRef(Guid value)
    {
        Value = value;
    }

    /// <summary>
    ///     Gets the reference GUID.
    /// </summary>
    public Guid Value { get; }

    /// <summary>
    ///     Gets a value indicating whether this is an empty 1C reference (all-zero GUID).
    /// </summary>
    public bool IsEmpty => Value == Empty;

    /// <summary>
    ///     Parses a raw 1C reference string into a <see cref="OneCRef" />.
    ///     Returns null for an empty reference (all-zero GUID) or null/empty input.
    ///     Throws <see cref="InvalidOperationException" /> if the value is not a valid GUID.
    /// </summary>
    /// <param name="raw">Raw reference string from 1C (e.g. "9d8fb587-9a93-11e6-80e0-1078d2d7c888").</param>
    /// <returns>A <see cref="OneCRef" />, or null if the reference is empty.</returns>
    /// <exception cref="InvalidOperationException">When the value is not a valid GUID.</exception>
    public static OneCRef? FromString(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (!Guid.TryParse(raw, out var guid))
        {
            throw new InvalidOperationException(
                $"Invalid 1C reference value '{raw}': expected a GUID (e.g. 9d8fb587-9a93-11e6-80e0-1078d2d7c888).");
        }

        return guid == Empty ? null : new OneCRef(guid);
    }

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}